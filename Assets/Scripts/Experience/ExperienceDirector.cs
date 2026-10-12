using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Admin.Sessions;
using DroneDDW.Characters;
using DroneDDW.NarratorOrb;
using DroneDDW.Neighborhood.Experience;
using UnityEngine;

namespace DroneDDW.Experience
{
	// Runs a participant's session, one awaited part after another (logged as [Experience]):
	// 1. Check-in: waits for the admin menu's Start session
	// 2. Handover: shows the admin "Give the headset to participant 104" and waits for the headset to come off and go
	//    back on (or A); then asks the participant to adjust the headset and waits until the admin holds A / X, and
	//    recentres the scene in front of them (Reset player location)
	// 3. Welcome: the orb appears and speaks
	// 4. Hand check: line both hands up with the ghost hands
	// 5. Practice choice: the table appears; moving the slider changes the orb; the orb drifts above the lock-in button,
	//    holding it locks the choice in (not recorded), and the orb drifts back
	// 6. Characters: the three cards appear and each says their line in turn, the orb waiting to their left
	// 7. Into the Neighborhood, behind a fade; the orb comes along
	// 8. The real choice: the slider changes the street; holding the button locks it in and records it (Id, Group,
	//    Form choice, Date, Time in Sessions.csv, through Session Control)
	// 9. Back to passthrough for the orb's outro, then goodbye
	// 10. When the headset comes off (or A), End session: the scene reloads on check-in for the next participant
	// Every step that waits for the participant gets the settings' stuck lines. Reset session (the same participant)
	// starts over without check-in and handover. While the session is paused the tables' hands are switched off, since
	// freezing time doesn't stop them.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Experience Director")]
	internal sealed class ExperienceDirector : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ExperienceSettings _settings;
		[SerializeField] private SessionControl _session;
		[SerializeField] private HeadsetPresence _presence;
		[SerializeField] private EnvironmentSwitch _environment;
		[SerializeField] private NoticePanel _notice;

		[Header("Narrator")]
		[SerializeField] private NarratorOrbController _orb;
		[SerializeField] private ExperienceProp _orbProp;

		[Tooltip("Where the orb floats in passthrough: above the table")]
		[SerializeField] private Transform _orbHome;

		[Tooltip("Where it floats in the Neighborhood, out of the way of the street")]
		[SerializeField] private Transform _orbNeighborhoodSpot;

		[Tooltip("Where it waits while the characters speak, out of the way of the cards")]
		[SerializeField] private Transform _orbCharactersSpot;

		[Tooltip("Where it floats during the practice choice: above the lock-in button, on the right")]
		[SerializeField] private Transform _orbButtonSpot;

		[Min(0.1f)]
		[SerializeField] private float _orbDriftSeconds = 1.5f;

		[Header("Steps")]
		[SerializeField] private HandAlignmentCondition _handCheck;
		[SerializeField] private PracticeChoice _practice;

		[Header("Characters")]
		[SerializeField] private ExperienceProp _cardsProp;

		[Tooltip("Grandma, student and worker, in the order they speak")]
		[SerializeField] private CharacterCard[] _cards;

		#endregion

		#region Fields

		// The orb's neutral look, between A and B
		private const int NeutralOrbStep = 2;

		private CancellationTokenSource _skipSource = new();
		private NeighborhoodExperience _neighborhood;
		private bool _isPracticeLockOpen;
		private bool _isNeighborhoodLockOpen;

		#endregion

		#region Properties

		internal ExperiencePhase Phase { get; private set; }

		// The Play mode check fast-forwards: no lines, no waiting for the participant, steps count as done
		internal bool IsFastForward { get; set; }

		// The row the session recorded, or null
		internal string RecordedChoice { get; private set; }

		#endregion

		#region Setup

		private void OnEnable()
		{
			_session.OnPause += HandlePauseChange;
			_session.OnResume += HandlePauseChange;
		}

		private void OnDisable()
		{
			_session.OnPause -= HandlePauseChange;
			_session.OnResume -= HandlePauseChange;
		}

		private void Start()
		{
			ApplyInput();
			RunAsync(destroyCancellationToken).Forget();
		}

		private void OnDestroy()
		{
			_skipSource.Cancel();
			_skipSource.Dispose();
		}

		#endregion

		#region Event handlers

		private void HandlePauseChange()
		{
			ApplyInput();
		}

		#endregion

		#region Internal

		internal void Configure(ExperienceSettings settings, SessionControl session, HeadsetPresence presence,
			EnvironmentSwitch environment, NoticePanel notice)
		{
			_settings = settings;
			_session = session;
			_presence = presence;
			_environment = environment;
			_notice = notice;
		}

		internal void ConfigureNarrator(NarratorOrbController orb, ExperienceProp orbProp, Transform orbHome,
			Transform orbNeighborhoodSpot, Transform orbCharactersSpot, Transform orbButtonSpot)
		{
			_orb = orb;
			_orbProp = orbProp;
			_orbHome = orbHome;
			_orbNeighborhoodSpot = orbNeighborhoodSpot;
			_orbCharactersSpot = orbCharactersSpot;
			_orbButtonSpot = orbButtonSpot;
		}

		internal void ConfigureSteps(HandAlignmentCondition handCheck, PracticeChoice practice, ExperienceProp cardsProp,
			CharacterCard[] cards)
		{
			_handCheck = handCheck;
			_practice = practice;
			_cardsProp = cardsProp;
			_cards = cards;
		}

		// Debug (N in the Editor): ends the line or step underway as if it were done
		internal void Skip()
		{
			Debug.Log($"[Experience] Skipping ahead ({Phase}).", this);
			_skipSource.Cancel();
		}

		#endregion

		#region Private

		private async UniTaskVoid RunAsync(CancellationToken cancellationToken)
		{
			try
			{
				await CheckInAsync(cancellationToken);
				if (!_session.IsRestart)
				{
					await HandoverAsync(cancellationToken);
				}

				await SettleAsync(cancellationToken);
				await WelcomeAsync(cancellationToken);
				await HandCheckAsync(cancellationToken);
				await PracticeChoiceAsync(cancellationToken);
				await CharactersAsync(cancellationToken);
				await ToNeighborhoodAsync(cancellationToken);
				await NeighborhoodChoiceAsync(cancellationToken);
				await ToPassthroughAsync(cancellationToken);
				await GoodbyeAsync(cancellationToken);
			}
			catch (OperationCanceledException)
			{
				// The scene closed (Reset session, End session, or leaving Play mode)
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
				Debug.LogError("[Experience] The session stopped after the error above. The admin menu's Reset session "
					+ "starts the participant over; End session goes back to check-in.", this);
			}
		}

		private async UniTask CheckInAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.CheckIn);
			if (!_session.HasParticipant && !_session.IsCheckingIn)
			{
				Debug.LogWarning("[Experience] Session Control has check-in off: running without a participant, so the "
					+ "choice won't be recorded.", this);
				return;
			}

			await UniTask.WaitUntil(() => _session.HasParticipant, cancellationToken: cancellationToken);
		}

		private async UniTask HandoverAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.Handover);
			_notice.Show(_settings.GetHandoverText(_session.ParticipantId));
			if (!IsFastForward)
			{
				await SkippableAsync(_presence.WaitForHandoverAsync, cancellationToken);
			}

			_notice.Hide();
		}

		// People adjust the headset before they are ready, so the admin starts when they are; the scene then recentres in
		// front of them as they now sit (Reset player location)
		private async UniTask SettleAsync(CancellationToken cancellationToken)
		{
			if (!IsFastForward)
			{
				_notice.Show(_settings.ReadyText);
				await SkippableAsync(WaitForStartAsync, cancellationToken);
				_notice.Hide();
			}

			_session.ResetPlayerLocation();
		}

		private async UniTask WelcomeAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.Welcome);
			_orbProp.Show();
			_orb.MoveTo(_orbHome.position);
			_orb.SetStep(NeutralOrbStep);
			await SayAsync(_settings.Welcome, cancellationToken);
		}

		private async UniTask HandCheckAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.HandCheck);
			await RunGuidedStepAsync("Hand check", _handCheck, _settings.HandCheck, cancellationToken);
		}

		private async UniTask PracticeChoiceAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.PracticeChoice);
			_practice.Show();

			await RunGuidedStepAsync("Move the slider", _practice.WaitForSliderMovedAsync, null, _settings.Slider,
				cancellationToken);

			// The orb shows the way to the button while it explains it, and comes back once the practice is locked in
			await DriftOrbAsync(_orbButtonSpot, cancellationToken);
			SetPracticeLockOpen(true);
			await RunGuidedStepAsync("Lock in the practice choice", _practice.WaitForLockInAsync, null,
				_settings.PracticeLockIn, cancellationToken);
			SetPracticeLockOpen(false);
			await DriftOrbAsync(_orbHome, cancellationToken);
		}

		private async UniTask CharactersAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.Characters);
			await SayAsync(_settings.CharactersIntro, cancellationToken);
			await DriftOrbAsync(_orbCharactersSpot, cancellationToken);

			// Shown first: a card reads its line ahead with what it sets up when it wakes
			_cardsProp.Show();
			for (int index = 0; index < _cards.Length; index++)
			{
				AudioClip line = _settings.GetCharacterLine(index);
				if (line != null)
				{
					_cards[index].Prepare(line);
				}
			}

			for (int speaker = 0; speaker < _cards.Length; speaker++)
			{
				for (int index = 0; index < _cards.Length; index++)
				{
					_cards[index].SetHighlighted(index == speaker);
				}

				await SayAsCharacterAsync(_cards[speaker], _settings.GetCharacterLine(speaker), cancellationToken);
				await PauseBetweenLinesAsync(cancellationToken);
			}

			foreach (CharacterCard card in _cards)
			{
				card.SetHighlighted(false);
			}

			await DriftOrbAsync(_orbHome, cancellationToken);
		}

		private async UniTask ToNeighborhoodAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.ToNeighborhood);
			await _environment.ToNeighborhoodAsync(_settings.FadeSeconds, _settings.BlackHoldSeconds,
				MoveOrbToNeighborhood, cancellationToken);
			_neighborhood = _environment.Neighborhood;
		}

		private async UniTask NeighborhoodChoiceAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.NeighborhoodChoice);
			if (_neighborhood == null)
			{
				Debug.LogError("[Experience] The Neighborhood didn't load, so there's no choice to record.", this);
				return;
			}

			await SayAsync(_settings.NeighborhoodWelcome, cancellationToken);
			_isNeighborhoodLockOpen = true;
			ApplyInput();
			await RunGuidedStepAsync("Lock in the choice", WaitForNeighborhoodLockInAsync, null,
				_settings.NeighborhoodLockIn, cancellationToken);
			_isNeighborhoodLockOpen = false;
			ApplyInput();

			// A skipped lock-in (debug, or the Play mode check) records what the street shows
			int version = (int)(_neighborhood.IsLocked ? _neighborhood.LockedVersion : _neighborhood.CurrentVersion);
			RecordedChoice = _settings.GetFormChoiceName(version);
			if (_session.HasParticipant)
			{
				_session.RecordChoice(RecordedChoice);
			}
			else
			{
				Debug.LogWarning($"[Experience] No participant, so {RecordedChoice} isn't recorded.", this);
			}
		}

		private async UniTask ToPassthroughAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.ToPassthrough);
			_neighborhood = null;
			await _environment.ToPassthroughAsync(_settings.FadeSeconds, _settings.BlackHoldSeconds, MoveOrbHome,
				cancellationToken);
			SetPhase(ExperiencePhase.Outro);
			await SayAsync(_settings.Outro, cancellationToken);
		}

		private async UniTask GoodbyeAsync(CancellationToken cancellationToken)
		{
			SetPhase(ExperiencePhase.Goodbye);
			_notice.Show(_settings.GoodbyeText);
			await SayAsync(_settings.Goodbye, cancellationToken);
			if (!IsFastForward)
			{
				await SkippableAsync(_presence.WaitForTakenOffAsync, cancellationToken);
			}

			SetPhase(ExperiencePhase.Ended);
			_session.EndSession();
		}

		private UniTask RunGuidedStepAsync(string name, ExperienceCondition condition, GuidedStepLines lines,
			CancellationToken cancellationToken)
		{
			return RunGuidedStepAsync(name, condition.WaitAsync, condition.ShowHint, lines, cancellationToken);
		}

		// The prompt, then waiting for the action (listening from the start of the prompt, so acting early counts)
		// with the stuck lines while it isn't done, then the success line. A debug skip ends it as if it were done.
		private async UniTask RunGuidedStepAsync(string name, Func<CancellationToken, UniTask> waitAsync,
			Action showHint, GuidedStepLines lines, CancellationToken cancellationToken)
		{
			Debug.Log($"[Experience] {name}: start", this);
			if (IsFastForward)
			{
				return;
			}

			using CancellationTokenSource stepSource =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _skipSource.Token);
			try
			{
				UniTask conditionTask = waitAsync(stepSource.Token).Preserve();
				await SayLineAsync(lines.Prompt, stepSource.Token);
				if (conditionTask.Status.IsCompleted())
				{
					await conditionTask;
				}
				else
				{
					await WaitWithHintsAsync(conditionTask, showHint, lines.Stuck, stepSource.Token);
				}
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				Debug.Log($"[Experience] {name}: skipped", this);
				RenewSkip();
			}

			Debug.Log($"[Experience] {name}: done", this);
			await SayAsync(lines.Success, cancellationToken);
		}

		private async UniTask WaitWithHintsAsync(UniTask conditionTask, Action showHint, AudioClip stuckClip,
			CancellationToken cancellationToken)
		{
			using CancellationTokenSource hintSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			RunHintsAsync(showHint, stuckClip, hintSource.Token).Forget();
			try
			{
				await conditionTask;
			}
			finally
			{
				// Cuts off a stuck line that is still playing
				hintSource.Cancel();
			}
		}

		// The stuck lines, until cancelled; on scaled time, so a paused session holds them back
		private async UniTaskVoid RunHintsAsync(Action showHint, AudioClip stuckClip, CancellationToken cancellationToken)
		{
			StuckHintSchedule schedule = new(_settings.FirstHintDelaySeconds, _settings.HintRepeatSeconds);
			try
			{
				for (int hint = 0; !cancellationToken.IsCancellationRequested; hint++)
				{
					await UniTask.Delay(TimeSpan.FromSeconds(schedule.GetDelaySeconds(hint)),
						cancellationToken: cancellationToken);
					Debug.Log($"[Experience] Stuck line {hint + 1}", this);
					showHint?.Invoke();
					await SayLineAsync(stuckClip, cancellationToken);
				}
			}
			catch (OperationCanceledException)
			{
				// The step is done, or the session closed
			}
		}

		// A line from the orb that a debug skip cuts short
		private UniTask SayAsync(AudioClip clip, CancellationToken cancellationToken)
		{
			return SkippableAsync(token => SayLineAsync(clip, token), cancellationToken);
		}

		// Speaks with the orb and waits for the line to end; cancelling cuts it off
		private async UniTask SayLineAsync(AudioClip clip, CancellationToken cancellationToken)
		{
			if (IsFastForward || clip == null)
			{
				return;
			}

			_orb.Speak(clip);
			try
			{
				await UniTask.WaitUntil(() => !_orb.IsSpeaking, cancellationToken: cancellationToken);
			}
			catch (OperationCanceledException)
			{
				_orb.StopSpeaking();
				throw;
			}
		}

		private async UniTask SayAsCharacterAsync(CharacterCard card, AudioClip clip, CancellationToken cancellationToken)
		{
			if (IsFastForward || clip == null)
			{
				return;
			}

			card.Speak(clip);
			try
			{
				await SkippableAsync(token => UniTask.WaitUntil(() => !card.IsSpeaking, cancellationToken: token),
					cancellationToken);
			}
			finally
			{
				card.Stop();
			}
		}

		private UniTask PauseBetweenLinesAsync(CancellationToken cancellationToken)
		{
			return IsFastForward
				? UniTask.CompletedTask
				: UniTask.Delay(TimeSpan.FromSeconds(_settings.PauseBetweenLinesSeconds),
					cancellationToken: cancellationToken);
		}

		private UniTask WaitForStartAsync(CancellationToken cancellationToken)
		{
			return _presence.WaitForStartHoldAsync(_settings.StartHoldSeconds, cancellationToken);
		}

		private async UniTask WaitForNeighborhoodLockInAsync(CancellationToken cancellationToken)
		{
			await UniTask.WaitUntil(() => _neighborhood.IsLocked, cancellationToken: cancellationToken);
		}

		// Awaits work that a debug skip may end early, as if it had finished
		private async UniTask SkippableAsync(Func<CancellationToken, UniTask> work, CancellationToken cancellationToken)
		{
			using CancellationTokenSource source =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _skipSource.Token);
			try
			{
				await work(source.Token);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				RenewSkip();
			}
		}

		private void RenewSkip()
		{
			if (!_skipSource.IsCancellationRequested)
			{
				return;
			}

			_skipSource.Dispose();
			_skipSource = new CancellationTokenSource();
		}

		private UniTask DriftOrbAsync(Transform spot, CancellationToken cancellationToken)
		{
			return IsFastForward
				? UniTask.CompletedTask
				: OrbDriftUtil.DriftAsync(_orb, spot.position, _orbDriftSeconds, cancellationToken);
		}

		// In the Neighborhood the orb isn't seen: its lines play as a stereo voice-over
		private void MoveOrbToNeighborhood()
		{
			_orb.MoveTo(_orbNeighborhoodSpot.position);
			_orb.SetVisible(false);
			_orb.SetSpatial(false);
		}

		private void MoveOrbHome()
		{
			_orb.MoveTo(_orbHome.position);
			_orb.SetVisible(true);
			_orb.SetSpatial(true);
		}

		private void SetPhase(ExperiencePhase phase)
		{
			Phase = phase;
			Debug.Log($"[Experience] {phase}", this);
			ApplyInput();
		}

		private void SetPracticeLockOpen(bool isOpen)
		{
			_isPracticeLockOpen = isOpen;
			ApplyInput();
		}

		// Which hands may move what: the practice slider and button in their part, the Neighborhood's in theirs, and
		// nothing while paused
		private void ApplyInput()
		{
			bool isRunning = !_session.IsPaused;
			bool isPractice = Phase == ExperiencePhase.PracticeChoice;
			_practice.SetSliderEnabled(isRunning && isPractice && !_practice.IsLocked);
			_practice.SetLockAvailable(isRunning && isPractice && _isPracticeLockOpen);
			if (_neighborhood != null)
			{
				bool isChoosing = Phase == ExperiencePhase.NeighborhoodChoice;
				_neighborhood.SetSliderEnabled(isRunning && isChoosing);
				_neighborhood.SetLockEnabled(isRunning && isChoosing && _isNeighborhoodLockOpen);
			}
		}

		#endregion
	}
}
