using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

using DropBoxSync.iOS;

namespace CameraSync
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the 
	// User Interface of the application, as well as listening (and optionally responding) to 
	// application events from iOS.
	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate
	{
		const string DropboxSyncKey = "wydjum77g1jmp3s";
		const string DropboxSyncSecret = "2etgbjftzgjrnjh";

		// class-level declarations
		CameraSyncViewController viewController;
		UIWindow window;
		//
		// This method is invoked when the application has loaded and is ready to run. In this 
		// method you should instantiate the window, load the UI into it and then make the window
		// visible.
		//
		// You have 17 seconds to return from this method, or iOS will terminate your application.
		//
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			// The account manager stores all the account info. Create this when your app launches
			var manager = new DBAccountManager (DropboxSyncKey, DropboxSyncSecret);
			DBAccountManager.SharedManager = manager;

			var account = manager.LinkedAccount;
			if (account != null) {
				var filesystem = new DBFilesystem (account);
				DBFilesystem.SharedFilesystem = filesystem;
			}	

			window = new UIWindow (UIScreen.MainScreen.Bounds);

			viewController = new CameraSyncViewController ();
			UINavigationController rootController = new UINavigationController (viewController);
			window.RootViewController = rootController;
			window.MakeKeyAndVisible ();
			return true;
		}

		public override bool OpenUrl (UIApplication application, NSUrl url, string sourceApplication, NSObject annotation)
		{
			var account = DBAccountManager.SharedManager.HandleOpenURL (url);
			if (account != null) {
				var filesystem = new DBFilesystem (account);
				DBFilesystem.SharedFilesystem = filesystem;
				viewController.DropboxObserver ();

				Console.WriteLine ("App linked successfully!");
				return true;
			} else {
				Console.WriteLine ("App is not linked");
				return false;
			}
		}
	}
}

