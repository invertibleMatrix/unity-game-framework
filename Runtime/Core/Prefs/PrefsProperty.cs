namespace AK.Core
{
	/// <summary>
	/// <see cref="PrefsProperty{TProp}"/> is a wrapper around <see cref="UniPrefs"/>'s API
	/// which save/load operations over a property... 
	/// </summary>
	/// <typeparam name="T">TypeOf property to wrap around, Make sure It's Serializable</typeparam>
	public sealed class PrefsProperty<T>
	{
		private T _current = default;
		private bool _isSyncWithPrefs = false;
		
		private readonly T _default = default;
		private readonly string _saveKey = default;

		/// <summary>
		/// Create & Returns the InstanceOf <see cref="PrefsProperty{TProp}"/> with the given SaveKey...
		/// </summary>
		/// <param name="saveKey"><see cref="string"/> Key To Use As Key In Database...</param>
		/// <param name="default">Default Value To Save On Creation...</param>
		public PrefsProperty(string saveKey, T @default = default)
		{
			_saveKey = saveKey;
			_default = @default;
			
			_current = @default;
			_isSyncWithPrefs = false;

			UniPrefs.OnReset += Reset;
		}

		~PrefsProperty() => UniPrefs.OnReset -= Reset;

		/// <summary>
		/// <see cref="Save"/> is going to save the given data in <see cref="UniPrefs"/>
		/// & also update <see cref="_current"/> runtime state...
		/// </summary>
		public void Save(T toSave = default)
		{
			if (toSave is not null)
			{
				_current = toSave;
			}

			_isSyncWithPrefs = true;
			UniPrefs.Set(_saveKey, _current);
		}

		/// <summary>
		/// <see cref="Read"/> this property data from save system & also update current runtime state...
		/// </summary>
		/// <returns>returns save data if exists...</returns>
		public T Read()
		{
			if (_isSyncWithPrefs) return _current;

			_isSyncWithPrefs = true;
			return _current = UniPrefs.Get(_saveKey, _current);
		}

		/// <summary>
		/// Reset this <see cref="PrefsProperty{T}"/> & delete it from <see cref="UniPrefs"/>...
		/// </summary>
		public void Reset()
		{
			if (UniPrefs.HasKey(_saveKey))
			{
				UniPrefs.Delete(_saveKey);
			}
			
			_current = _default;
			_isSyncWithPrefs = false;
		}

		/// <summary>
		/// <see cref="ToString"/> override for <see cref="PrefsProperty{TProp}"/> which converts <see cref="_current"/>'s
		/// <see cref="ToString"/> & Returns...
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return _current == null ? string.Empty : _current.ToString();
		}

		/// <summary>
		/// an implicit operator overload to get <see cref="_current"/> state of this property...
		/// </summary>
		public static implicit operator T(PrefsProperty<T> property) => property.Read();
	}
}