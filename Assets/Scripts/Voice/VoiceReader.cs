using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Voice
{
	// Reads a voice from an AudioSource (the Narrator Orb web preview's loadClip, reanalyze, position and the onset
	// loop in frame()). A clip that starts playing is analysed once, off the main thread, and cached; each frame then
	// samples the features at the playhead plus the lead, and reports the syllables the playhead passed. A clip that
	// can't be read ahead falls back to the live tap.
	public sealed class VoiceReader : IDisposable
	{
		#region Events

		// A syllable the playhead passed, with its strength (0.3 to 1)
		public event Action<float> OnSyllable;

		#endregion

		#region Fields

		// Re-analysis waits until the analysis settings stay put this long, like the preview's timer
		private const float SettleSeconds = 0.12f;

		// A playhead further than this from where it should be has been moved: a seek, a restart or a loop
		private const double JumpToleranceSeconds = 0.25;

		private readonly AudioSource _source;
		private readonly VoiceLiveTap _liveTap;
		private readonly CancellationTokenSource _lifetimeSource = new();
		private readonly Dictionary<AudioClip, VoiceAnalysis> _analyses = new();
		private readonly HashSet<AudioClip> _unreadableClips = new();
		private readonly List<double> _liveOnsetStrengths = new();
		private CancellationTokenSource _analysisSource;
		private AudioClip _analysingClip;
		private VoiceAnalysisSettings _analysingSettings;
		private VoiceAnalysisSettings _analysisSettings;
		private VoiceAnalysisSettings _pendingSettings;
		private float _settleRemainingSeconds = -1f;
		private bool _hasSettings;
		private AudioClip _clip;
		private bool _isSynced;
		private int _onsetIndex;
		private double _lastPlayheadSeconds;

		#endregion

		#region Properties

		public VoiceStatus Status { get; private set; }

		// The analysis of the clip being followed, or null before it is done
		public VoiceAnalysis Analysis
		{
			get
			{
				if (_clip == null || !_analyses.TryGetValue(_clip, out VoiceAnalysis analysis))
				{
					return null;
				}

				return analysis;
			}
		}

		#endregion

		#region Setup

		// The live tap is optional: without one, clips that can't be read ahead leave the features at zero
		public VoiceReader(AudioSource source, VoiceLiveTap liveTap)
		{
			_source = source;
			_liveTap = liveTap;
		}

		public void Dispose()
		{
			_analysisSource?.Cancel();
			_analysisSource?.Dispose();
			_lifetimeSource.Cancel();
			_lifetimeSource.Dispose();
			EndLive();
		}

		#endregion

		#region Public

		// The voice features at the moment being heard, plus the lead, and OnSyllable for every syllable passed since
		// the last call. Call once per frame with the real time that passed. A change in the analysis settings re-runs
		// the analysis once they have stayed put for a moment.
		public VoiceFeatures Read(VoiceAnalysisSettings settings, double leadSeconds, float unscaledDeltaSeconds)
		{
			SettleSettings(settings, unscaledDeltaSeconds);

			AudioClip clip = _source != null ? _source.clip : null;
			if (clip == null || !_source.isPlaying)
			{
				_isSynced = false;
				Status = VoiceStatus.Idle;
				EndLive();
				return VoiceFeatures.Silent;
			}

			if (clip != _clip)
			{
				_clip = clip;
				_isSynced = false;
			}

			if (_unreadableClips.Contains(clip))
			{
				return ReadLive();
			}

			EndLive();
			VoiceAnalysis analysis = Analysis;
			if (analysis == null || !analysis.Settings.Equals(_analysisSettings))
			{
				StartAnalysis(clip);
			}

			if (analysis == null)
			{
				Status = VoiceStatus.Analysing;
				return VoiceFeatures.Silent;
			}

			Status = VoiceStatus.ClipAnalysis;
			return ReadAnalysis(analysis, clip, leadSeconds, unscaledDeltaSeconds);
		}

		// Analyses a clip before it plays, so it is read from its first syllable. Otherwise the analysis starts with
		// the clip, and the syllables that pass while it runs are skipped. Does nothing while another clip is being
		// analysed.
		public void Prepare(AudioClip clip, VoiceAnalysisSettings settings)
		{
			SettleSettings(settings, 0f);
			bool isAnalysed = _analyses.TryGetValue(clip, out VoiceAnalysis analysis)
				&& analysis.Settings.Equals(_analysisSettings);
			bool isBusy = _analysingClip != null && _analysingClip != clip;
			if (!isAnalysed && !isBusy && !_unreadableClips.Contains(clip))
			{
				StartAnalysis(clip);
			}
		}

		#endregion

		#region Private

		private void SettleSettings(VoiceAnalysisSettings settings, float unscaledDeltaSeconds)
		{
			if (!_hasSettings)
			{
				_hasSettings = true;
				_analysisSettings = settings;
				_pendingSettings = settings;
				return;
			}

			if (!settings.Equals(_pendingSettings))
			{
				_pendingSettings = settings;
				_settleRemainingSeconds = SettleSeconds;
				return;
			}

			if (_settleRemainingSeconds < 0f)
			{
				return;
			}

			_settleRemainingSeconds -= unscaledDeltaSeconds;
			if (_settleRemainingSeconds < 0f)
			{
				_analysisSettings = _pendingSettings;
			}
		}

		private VoiceFeatures ReadAnalysis(VoiceAnalysis analysis, AudioClip clip, double leadSeconds,
			float unscaledDeltaSeconds)
		{
			double playheadSeconds = (double)_source.timeSamples / clip.frequency;
			double aheadSeconds = playheadSeconds + leadSeconds;
			double expectedSeconds = _lastPlayheadSeconds + unscaledDeltaSeconds * _source.pitch;
			if (!_isSynced || Math.Abs(playheadSeconds - expectedSeconds) > JumpToleranceSeconds)
			{
				// Started, resumed, seeked, looped or re-analysed: carry on from here without firing what was skipped
				_onsetIndex = analysis.FirstOnsetAfter(playheadSeconds);
				_isSynced = true;
			}

			_lastPlayheadSeconds = playheadSeconds;
			IReadOnlyList<VoiceOnset> onsets = analysis.Onsets;
			while (_onsetIndex < onsets.Count && onsets[_onsetIndex].TimeSeconds <= aheadSeconds)
			{
				OnSyllable?.Invoke((float)onsets[_onsetIndex].Strength);
				_onsetIndex++;
			}

			return analysis.Sample(aheadSeconds);
		}

		private VoiceFeatures ReadLive()
		{
			if (_liveTap == null)
			{
				Status = VoiceStatus.Idle;
				return VoiceFeatures.Silent;
			}

			Status = VoiceStatus.LiveFallback;
			_liveTap.Begin(_analysisSettings);
			VoiceFeatures features = _liveTap.Read(_liveOnsetStrengths);
			foreach (double strength in _liveOnsetStrengths)
			{
				OnSyllable?.Invoke((float)strength);
			}

			_liveOnsetStrengths.Clear();
			return features;
		}

		private void EndLive()
		{
			if (_liveTap != null && _liveTap.IsRunning)
			{
				_liveTap.End();
			}
		}

		private void StartAnalysis(AudioClip clip)
		{
			if (_analysingClip == clip && _analysingSettings.Equals(_analysisSettings))
			{
				return;
			}

			_analysisSource?.Cancel();
			_analysisSource?.Dispose();
			_analysisSource = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeSource.Token);
			_analysingClip = clip;
			_analysingSettings = _analysisSettings;
			AnalyseAsync(clip, _analysisSettings, _analysisSource.Token).Forget();
		}

		private async UniTaskVoid AnalyseAsync(AudioClip clip, VoiceAnalysisSettings settings,
			CancellationToken cancellationToken)
		{
			try
			{
				float[] samples = await ReadSamplesAsync(clip, cancellationToken);
				if (samples == null)
				{
					_unreadableClips.Add(clip);
					if (_liveTap == null)
					{
						Debug.LogWarning($"[Voice] '{clip.name}' can't be read ahead and there is no live tap on the "
							+ "voice's object, so the voice reads as silent while this clip plays.", clip);
					}

					return;
				}

				int channelCount = clip.channels;
				int sampleRate = clip.frequency;
				VoiceAnalysis analysis = await UniTask.RunOnThreadPool(
					() => VoiceAnalysis.Analyze(VoiceAnalysis.ToMono(samples, channelCount), sampleRate, settings),
					cancellationToken: cancellationToken);

				// Back on the main thread, where a newer analysis may have taken over in the meantime
				cancellationToken.ThrowIfCancellationRequested();
				_analyses[clip] = analysis;
				if (clip == _clip)
				{
					_isSynced = false;
				}

				Debug.Log($"[Voice] Analysed '{clip.name}': {analysis.Onsets.Count} syllables, reference level "
					+ $"{analysis.ReferenceFullDecibels:F1} dB.", clip);
			}
			catch (OperationCanceledException)
			{
				// A newer analysis took over, or the reader was disposed
			}
			finally
			{
				if (_analysingClip == clip && _analysingSettings.Equals(settings))
				{
					_analysingClip = null;
				}
			}
		}

		// The clip's samples (channels interleaved), or null when it can't be read ahead
		private static async UniTask<float[]> ReadSamplesAsync(AudioClip clip, CancellationToken cancellationToken)
		{
			if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
			{
				Debug.LogWarning($"[Voice] Can't read '{clip.name}' ahead: its Load Type is {clip.loadType}. Set Load "
					+ "Type to Decompress On Load in its import settings so it can be analysed. Until then the voice "
					+ "is read from the live output, which reacts a little later and learns the levels as it goes.",
					clip);
				return null;
			}

			if (clip.loadState != AudioDataLoadState.Loaded)
			{
				clip.LoadAudioData();
				await UniTask.WaitUntil(() => clip.loadState is AudioDataLoadState.Loaded or AudioDataLoadState.Failed,
					cancellationToken: cancellationToken);
			}

			float[] samples = new float[clip.samples * clip.channels];
			if (clip.loadState != AudioDataLoadState.Loaded || !clip.GetData(samples, 0))
			{
				Debug.LogWarning($"[Voice] Couldn't read the samples of '{clip.name}', so the voice is read from its "
					+ "live output instead.", clip);
				return null;
			}

			return samples;
		}

		#endregion
	}
}
