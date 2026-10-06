using System;
using AK.Services;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Notifications
{
	/// <summary>The notification service comes from the Mobile Notifications adapter when it is loaded, and is the no-op one otherwise.</summary>
	public class NotificationServiceFactoryTests
	{
		private Func<GameObject, INotificationService> _registered;
		private GameObject _host;

		[SetUp]
		public void SetUp()
		{
			_registered = NotificationServiceFactory.MobileNotifications;
			_host = new GameObject(nameof(NotificationServiceFactoryTests));
		}

		[TearDown]
		public void TearDown()
		{
			NotificationServiceFactory.MobileNotifications = _registered;
			Object.DestroyImmediate(_host);
		}

		[Test]
		public void Create_WithoutTheAdapter_IsTheNoOpService()
		{
			NotificationServiceFactory.MobileNotifications = null;

			Assert.IsInstanceOf<NullNotificationService>(NotificationServiceFactory.Create(_host));
		}

		[Test]
		public void Create_WithTheAdapter_PutsItsServiceOnTheHost()
		{
			var service = new NullNotificationService();
			GameObject given = null;
			NotificationServiceFactory.MobileNotifications = host =>
			{
				given = host;
				return service;
			};

			Assert.AreSame(service, NotificationServiceFactory.Create(_host));
			Assert.AreSame(_host, given);
		}

		[Test]
		public void Create_RejectsAMissingOrDestroyedHost()
		{
			var destroyed = new GameObject(nameof(Create_RejectsAMissingOrDestroyedHost));
			Object.DestroyImmediate(destroyed);

			Assert.Throws<ArgumentNullException>(() => NotificationServiceFactory.Create(null));
			Assert.Throws<ArgumentNullException>(() => NotificationServiceFactory.Create(destroyed));
		}
	}
}
