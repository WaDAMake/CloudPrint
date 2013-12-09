using System;
using System.Drawing;
using System.Timers;
using System.Threading.Tasks;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

using Xamarin.Media;
using DropBoxSync.iOS;

namespace CameraSync
{
	public partial class CameraSyncViewController : UICollectionViewController
	{
		private ContactViewController ContactDetails;

		public CameraSyncViewController () : base(new UICollectionViewFlowLayout())
		{
			Title = "照片";
			ContactDetails = new ContactViewController ();
			UICollectionViewFlowLayout flowLayout = (UICollectionViewFlowLayout)Layout;

			if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone) {
				flowLayout.ItemSize = new SizeF (75, 100);
				flowLayout.MinimumInteritemSpacing = 6;
				flowLayout.MinimumLineSpacing = 6;
			} else {
				flowLayout.ItemSize = new SizeF (180, 240);
				flowLayout.MinimumInteritemSpacing = 16;
				flowLayout.MinimumLineSpacing = 16;
			}
		}

		public override void DidReceiveMemoryWarning ()
		{
			// Releases the view if it doesn't have a superview.
			base.DidReceiveMemoryWarning ();
			
			// Release any cached data, images, etc that aren't in use.
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			CheckDropboxLinked ();

			NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Camera, (s, t) => {
				ShowCamera();
			});

			CollectionView.RegisterClassForCell (typeof(PhotoCell), PhotoCell.Key);

			if (DBFilesystem.SharedFilesystem != null) {
				DropboxObserver ();
			}
		}

		#region Helper Methods
		private void CheckDropboxLinked()
		{
			if (DBAccountManager.SharedManager.LinkedAccount == null) {
				DBAccountManager.SharedManager.LinkFromController (this);
			}
		}

		// resize the image (without trying to maintain aspect ratio)
		static UIImage ResizeImage(UIImage sourceImage, float scale)
		{
			float width = sourceImage.Size.Width * scale;
			float height = sourceImage.Size.Height * scale;
			UIGraphics.BeginImageContext(new SizeF(width, height));
			sourceImage.Draw(new RectangleF(0, 0, width, height));
			var resultImage = UIGraphics.GetImageFromCurrentImageContext();
			UIGraphics.EndImageContext();
			return resultImage;
		}

		private bool ObserverRegistered = false;
		public void DropboxObserver()
		{
			if (!ObserverRegistered && DBFilesystem.SharedFilesystem != null) {
				DBFilesystem.SharedFilesystem.AddObserverForPathAndChildren (this, DBPath.Root, () => {
					Console.WriteLine("Filesystem changed !");
					RefreshAlbums();
				});
				ObserverRegistered = true;

				ContactViewController.OpenDatastore ();
			}
		}
		#endregion

		#region UI Methods
		void ShowCamera ()
		{
			CheckDropboxLinked();

			var picker = new MediaPicker();
			MediaPickerController controller = picker.GetTakePhotoUI (new StoreCameraMediaOptions {
				Name = "latest.jpg",
				Directory = "Shooter"
			});

			// On iPad, you'll use UIPopoverController to present the controller
			PresentViewController (controller, true, null);
			controller.GetResultAsync().ContinueWith (t => {
				controller.DismissViewControllerAsync(true).ContinueWith( (t2) => {
					// Move to Dropbox.
					if (t.Status == TaskStatus.RanToCompletion) {
						MediaFile photoFile = t.Result;

						UIImage originalImage = new UIImage(photoFile.Path);
						NSData imageData = ResizeImage(originalImage, 0.3f).AsJPEG();

						DBPath path = new DBPath(string.Format("{0:yyyy-MM-dd-HH-mm-ss}.jpg", DateTime.Now));
						DBError err;
						DBFile file = DBFilesystem.SharedFilesystem.CreateFile(path, out err);
						file.WriteDataAsync(imageData).ContinueWith(t3 => {
							file.Close();
						});

						Task.Run(() => {
							System.Threading.Thread.Sleep(1000);
						}).ContinueWith((t3) => {
							ShowCamera();
						}, TaskScheduler.FromCurrentSynchronizationContext());
					}
				});
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void RefreshAlbums()
		{
			DBFilesystem.SharedFilesystem.ListFolderAsync (DBPath.Root).ContinueWith (t => {
				Photos = t.Result;
				this.CollectionView.ReloadData ();
			}, TaskScheduler.FromCurrentSynchronizationContext ());
		}
		#endregion

		private DBFileInfo[] Photos = null;

		#region CollectionView Datasource Methods
		public override UICollectionViewCell GetCell (UICollectionView collectionView, NSIndexPath indexPath)
		{
			DBError err;
			PhotoCell cell = (PhotoCell)collectionView.DequeueReusableCell(PhotoCell.Key, indexPath);

			DBPath path = Photos [Photos.Length - indexPath.Row - 1].Path;
			DBFile file = DBFilesystem.SharedFilesystem.OpenFile (path, out err);
			UIImage image = null;

			// This means the file doesn't exist, or it is open with asynchronous operation.
			if (file == null) {
				cell.Content = null;
				return cell;
			}

			if (file.Status.Cached) {
				image = new UIImage (file.ReadData (out err));
				file.Close ();
			} else {
				file.AddObserver (this, () => {
					DBFileStatus newStatus = file.NewerStatus;

					if ((newStatus == null && file.Status.Cached) ||
					    (newStatus != null && newStatus.Cached)) {
						image = new UIImage (file.ReadData (out err));
						cell.Content = image;
						file.RemoveObserver(this);
						file.Close ();
					}
				});
			}
			cell.Content = image;

			return cell;
		}

		public override int GetItemsCount (UICollectionView collectionView, int section)
		{
			if (Photos == null) {
				if (DBFilesystem.SharedFilesystem != null) {
					RefreshAlbums ();
				}
				return 0;
			}
			return Photos.Length;
		}

//		public override UICollectionReusableView GetViewForSupplementaryElement (UICollectionView collectionView, NSString elementKind, NSIndexPath indexPath)
//		{
//		}

		public override int NumberOfSections (UICollectionView collectionView)
		{
			return 1;
		}
		#endregion

		#region CollectionView Delegate Methods
		public override void ItemSelected (UICollectionView collectionView, NSIndexPath indexPath)
		{
			ContactDetails.ContactKey = Photos [Photos.Length - indexPath.Row - 1].Path.Name;
			NavigationController.PushViewController (ContactDetails, true);
		}
		#endregion
	}
	
	public class PhotoCell : UICollectionViewCell
	{
		public static readonly NSString Key = new NSString("PhotoCell");

		private UILabel Downloading;

		[Export ("initWithFrame:")]
		public PhotoCell (RectangleF frame) : base (frame)
		{
			Downloading = new UILabel(new RectangleF(new PointF(0, 0), frame.Size)) {
				TextAlignment = UITextAlignment.Center,
				Text = "下載中",
				Hidden = true,
				BackgroundColor = UIColor.White,
			};

			ContentView.AddSubview (Downloading);
		}

		public UIImage Content {
			set {
				if (value == null) {
					ContentView.Layer.Contents = null;
					Downloading.Hidden = false;
				} else {
					ContentView.Layer.Contents = value.CGImage;
					Downloading.Hidden = true;
				}
			}
		}
	}
}

