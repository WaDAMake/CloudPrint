using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;

using DropBoxSync.iOS;

namespace CameraSync
{
	public partial class ContactViewController : DialogViewController
	{
		// Shared contants and properties.
		private static readonly string PromptString = "點選即可輸入";
		private static DBDatastore Datastore = null;
		private static DBTable ContactTable = null;
		private static NSDictionary FieldsOfRecord = null;

		private static readonly string KeyForName = "Name";
		private static readonly string KeyForPhone = "Phone";
		private static readonly string KeyForEmail = "Email";
		private static readonly string KeyForAddress = "Address";

		// Current contact properties.
		private DBRecord CurrentRecord = null;
		private EntryElement ContactEntry = null;
		private EntryElement PhoneEntry = null;
		private EntryElement EmailEntry = null;
		private EntryElement AddressEntry = null;
		private UIViewElement PhotoEntry = null;
		private UIImageView Photo = null;

		private string ContactValue = null;
		private string PhoneValue = null;
		private string EmailValue = null;
		private string AddressValue = null;

		public ContactViewController () : base (UITableViewStyle.Grouped, null, true)
		{
			ContactEntry = new EntryElement ("姓名", PromptString, String.Empty);
			PhoneEntry = new EntryElement ("電話", PromptString, String.Empty) {
				KeyboardType = UIKeyboardType.PhonePad
			};
			EmailEntry = new EntryElement ("Email", PromptString, String.Empty) {
				KeyboardType = UIKeyboardType.EmailAddress
			};
			AddressEntry = new EntryElement ("地址", PromptString, String.Empty);

			RectangleF Rect;

			if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone)
				Rect = new RectangleF (10, 10, 300, 400);
			else
				Rect = new RectangleF (9, 9, 750, 1000);

			Photo = new UIImageView (Rect);
			PhotoEntry = new UIViewElement ("照片", Photo, true);

			Root = new RootElement ("詳細") {
				new Section ("聯絡人") {
					ContactEntry,
					PhoneEntry,
					EmailEntry,
					AddressEntry,
				},
				new Section("照片") {
					PhotoEntry,
				},
			};

			NavigationItem.RightBarButtonItem = new UIBarButtonItem (UIBarButtonSystemItem.Action);

			OpenDatastore ();

			if (FieldsOfRecord == null) {
				FieldsOfRecord = new NSDictionary(KeyForName, "", KeyForPhone, "", KeyForEmail, "", KeyForAddress, "");
			}
		}

		private static readonly uint DatastoreNeedSync = (uint)DBDatastoreStatus.Incoming | (uint)DBDatastoreStatus.Outgoing;
		public override void ViewWillDisappear(bool animated) 
		{
			base.ViewWillDisappear (animated);

			CheckAndUpdateField (ContactEntry, ContactValue, KeyForName);
			CheckAndUpdateField (PhoneEntry, PhoneValue, KeyForPhone);
			CheckAndUpdateField (EmailEntry, EmailValue, KeyForEmail);
			CheckAndUpdateField (AddressEntry, AddressValue, KeyForAddress);

			if (((uint)Datastore.Status & DatastoreNeedSync) > 0) {
				Datastore.SyncAsync ().ContinueWith (t => {
					if (t.Status == TaskStatus.Faulted) {
						UIAlertView alert = new UIAlertView ("雲相機", "聯絡資料同步失敗，請確認是否有連接上網路！", null, "確認", null);
						alert.Show ();
					}
				}, TaskScheduler.FromCurrentSynchronizationContext());
			}
		}

		public bool IsDatastoreReady {
			get {
				return (Datastore != null);
			}
		}

		public string ContactKey {
			set {
				DBError err;

				if (((uint)Datastore.Status & DatastoreNeedSync) > 0) {
					Datastore.Sync (out err);
				}

				bool inserted = false;
				CurrentRecord = ContactTable.GetOrInsertRecord (value, FieldsOfRecord, inserted, out err);

				if (CurrentRecord != null) {
					ContactEntry.Value = ContactValue = CurrentRecord.ObjectForKey (KeyForName).ToString ();
					PhoneEntry.Value = PhoneValue = CurrentRecord.ObjectForKey (KeyForPhone).ToString ();
					EmailEntry.Value = EmailValue = CurrentRecord.ObjectForKey (KeyForEmail).ToString ();
					AddressEntry.Value = AddressValue = CurrentRecord.ObjectForKey (KeyForAddress).ToString ();

					if (ContactValue == null)
						ContactValue = "";
					if (PhoneValue == null)
						PhoneValue = "";
					if (EmailValue == null)
						EmailValue = "";
					if (AddressValue == null)
						AddressValue = "";
				} else {
					ContactEntry.Value = ContactValue = "";
					PhoneEntry.Value = PhoneValue = "";
					EmailEntry.Value = EmailValue = "";
					AddressEntry.Value = AddressValue = "";
				}

				DBFile file = DBFilesystem.SharedFilesystem.OpenFile (new DBPath (value), out err);
				if (file != null) {
					Photo.Image = new UIImage (file.ReadData (out err));
				}
			}
		}

		#region Helper Methods	
		private void CheckAndUpdateField(EntryElement entry, string originalValue, string keyForField)
		{
			if (entry.Value != originalValue) {
				if (entry.Value.Length == 0)
					CurrentRecord.RemoveObject (keyForField);
				else
					CurrentRecord.SetObject (new NSString(entry.Value), keyForField);
			}
		}

		public static void OpenDatastore()
		{
			if (Datastore == null && DBAccountManager.SharedManager.LinkedAccount != null) {
				DBError err;

				Datastore = DBDatastore.OpenDefaultStoreForAccount (DBAccountManager.SharedManager.LinkedAccount, out err);
				Datastore.Sync (out err);
				ContactTable = Datastore.GetTable("Contact");
			}
		}
		#endregion

	}
}
