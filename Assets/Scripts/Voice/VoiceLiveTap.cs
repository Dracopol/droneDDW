using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Voice
{
	// Follows an AudioSource's live output for clips a VoiceReader can't read ahead. It must sit on the AudioSource's
	// object: Unity hands that source's audio to OnAudioFilterRead, on the audio thread, where LiveVoice runs. Off
	// until the reader needs it, and the audio passes through unchanged.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Voice/Voice Live Tap")]
	public sealed class VoiceLiveTap : MonoBehaviour
	{
		#region Fields

		private readonly object _gate = new();
		private readonly List<double> _pendingOnsets = new();
		private readonly List<double> _audioThreadOnsets = new();
		private int _sampleRate;

		// Guarded by _gate
		private bool _isRequested;
		private bool _hasNewSettings;
		private VoiceAnalysisSettings _requestedSettings;
		private VoiceFeatures _latest;

		// Audio thread only
		private LiveVoice _liveVoice;
		private float[] _monoBuffer = new float[0];

		#endregion

		#region Properties

		internal bool IsRunning
		{
			get
			{
				lock (_gate)
				{
					return _isRequested;
				}
			}
		}

		#endregion

		#region Setup

		private void Awake()
		{
			_sampleRate = AudioSettings.outputSampleRate;
		}

		#endregion

		#region Update

		// Audio thread
		private void OnAudioFilterRead(float[] data, int channels)
		{
			LiveVoice liveVoice;
			lock (_gate)
			{
				if (!_isRequested)
				{
					_liveVoice = null;
					return;
				}

				if (_hasNewSettings || _liveVoice == null)
				{
					_liveVoice = new LiveVoice(_sampleRate, _requestedSettings);
					_hasNewSettings = false;
				}

				liveVoice = _liveVoice;
			}

			int frameCount = data.Length / channels;
			if (_monoBuffer.Length < frameCount)
			{
				_monoBuffer = new float[frameCount];
			}

			for (int frame = 0; frame < frameCount; frame++)
			{
				float sum = 0f;
				for (int channel = 0; channel < channels; channel++)
				{
					sum += data[frame * channels + channel];
				}

				_monoBuffer[frame] = sum / channels;
			}

			liveVoice.Push(_monoBuffer, frameCount);
			liveVoice.TakeOnsets(_audioThreadOnsets);
			lock (_gate)
			{
				_latest = liveVoice.Latest;
				_pendingOnsets.AddRange(_audioThreadOnsets);
			}

			_audioThreadOnsets.Clear();
		}

		#endregion

		#region Internal

		// Starts following the output with these settings (again from scratch when they change)
		internal void Begin(VoiceAnalysisSettings settings)
		{
			lock (_gate)
			{
				if (_isRequested && settings.Equals(_requestedSettings))
				{
					return;
				}

				_requestedSettings = settings;
				_hasNewSettings = true;
				_isRequested = true;
				_latest = VoiceFeatures.Silent;
				_pendingOnsets.Clear();
			}
		}

		internal void End()
		{
			lock (_gate)
			{
				_isRequested = false;
				_latest = VoiceFeatures.Silent;
				_pendingOnsets.Clear();
			}
		}

		// The latest features, and moves the syllables found since the last call into the list
		internal VoiceFeatures Read(List<double> onsetStrengths)
		{
			lock (_gate)
			{
				onsetStrengths.AddRange(_pendingOnsets);
				_pendingOnsets.Clear();
				return _latest;
			}
		}

		#endregion
	}
}
