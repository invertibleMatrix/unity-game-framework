using System;
using AK.Core;
using UnityEngine;

namespace GameplayCore.Models
{
	[Serializable]
	public class GameSettingsModel : EntityModel
	{
		[SerializeField] private float _musicVolume   = 1f;
		[SerializeField] private float _sfxVolume     = 1f;
		[SerializeField] private bool  _hapticEnabled = true;
		[SerializeField] private bool  _notificationsEnabled;

		public event Action OnChanged;

		public float MusicVolume
		{
			get => _musicVolume;
			set
			{
				_musicVolume = value;
				OnChanged?.Invoke();
			}
		}

		public float SFXVolume
		{
			get => _sfxVolume;
			set
			{
				_sfxVolume = value;
				OnChanged?.Invoke();
			}
		}

		public bool HapticsEnabled
		{
			get => _hapticEnabled;
			set
			{
				_hapticEnabled = value;
				OnChanged?.Invoke();
			}
		}

		public bool NotificationsEnabled
		{
			get => _notificationsEnabled;
			set
			{
				_notificationsEnabled = value;
				OnChanged?.Invoke();
			}
		}
	}
}