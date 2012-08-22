using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Social
{
	class FormAuthenticatorController : UITableViewController
	{
		FormAuthenticator authenticator;

		StatusView status;

		public FormAuthenticatorController (FormAuthenticator authenticator)
			: base (UITableViewStyle.Grouped)
		{
			this.authenticator = authenticator;

			Title = authenticator.Service.Title;

			TableView.DataSource = new FormDataSource (this);
			TableView.Delegate = new FormDelegate (this);

			NavigationItem.LeftBarButtonItem = new UIBarButtonItem (
				UIBarButtonSystemItem.Cancel,
				delegate { HandleCancel (); });
		}

		void HandleSubmit ()
		{
			if (status == null) {
				status = new StatusView ();
				NavigationItem.TitleView = status;
				status.StartAnimating ();
			}

			authenticator.SignInAsync ().ContinueWith (task => {

				StopStatus ();

				if (task.IsFaulted) {
					ShowError (task.Exception);
				}
				else {
					authenticator.OnSuccess (task.Result);
				}

			}, TaskScheduler.FromCurrentSynchronizationContext ());
		}

		void ShowError (Exception error)
		{
			var mainBundle = NSBundle.MainBundle;
			
			var alert = new UIAlertView (
				mainBundle.LocalizedString ("Error Signing In", "Error message title when failed to sign in"),
				mainBundle.LocalizedString (error.GetUserMessage (), "Error"),
				null,
				mainBundle.LocalizedString ("OK", "Dismiss button title when failed to sign in"));

			alert.Show ();
		}

		void HandleCancel ()
		{
			StopStatus ();
			authenticator.OnFailure (AuthenticationResult.Cancelled);
		}

		void StopStatus ()
		{
			if (status != null) {
				status.StopAnimating ();
				NavigationItem.TitleView = null;
				status = null;
			}
		}

		class StatusView : UIView
		{
			UIActivityIndicatorView activity;

			public StatusView ()
				: base (new RectangleF (0, 0, 180, 44))
			{
				BackgroundColor = UIColor.Clear;

				activity = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.White) {
					Frame = new RectangleF (0, 11.5f, 21, 21),
					HidesWhenStopped = false,
					Hidden = false,
				};
				AddSubview (activity);

				var label = new UILabel () {
					Text = NSBundle.MainBundle.LocalizedString ("Verifying", "Verifying status message when adding accounts"),
					TextColor = UIColor.White,
					Font = UIFont.BoldSystemFontOfSize (20),
					BackgroundColor = UIColor.Clear,
					Frame = new RectangleF (25, 0, Frame.Width - 25, 44),
				};
				AddSubview (label);

				var f = Frame;
				f.Width = label.Frame.X + label.StringSize (label.Text, label.Font).Width;
				Frame = f;
			}

			public void StartAnimating ()
			{
				activity.StartAnimating ();
			}

			public void StopAnimating ()
			{
				activity.StopAnimating ();
			}
		}

		class FormDelegate : UITableViewDelegate
		{
			FormAuthenticatorController controller;

			public FormDelegate (FormAuthenticatorController controller)
			{
				this.controller = controller;
			}

			public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
			{
				tableView.ResignFirstResponder ();

				if (indexPath.Section == 1) {
					tableView.DeselectRow (indexPath, true);
					((FormDataSource)tableView.DataSource).ResignFirstResponder ();
					controller.HandleSubmit ();
				}
				else if (indexPath.Section == 2) {
					tableView.DeselectRow (indexPath, true);
					UIApplication.SharedApplication.OpenUrl (
						new NSUrl (controller.authenticator.Service.CreateAccountLink.AbsoluteUri));

				}
			}
		}

		class FieldCell : UITableViewCell
		{
			public static readonly UIFont LabelFont = UIFont.BoldSystemFontOfSize (16);
			public static readonly UIFont FieldFont = UIFont.SystemFontOfSize (16);

			static readonly UIColor FieldColor = UIColor.FromRGB (56, 84, 135);

			public UITextField TextField { get; private set; }

			public FieldCell (FormAuthenticatorField field, float fieldXPosition, Action handleReturn)
				: base (UITableViewCellStyle.Default, "Field")
			{
				SelectionStyle = UITableViewCellSelectionStyle.None;

				TextLabel.Text = field.Title;

				var hang = 3;
				var h = FieldFont.PointSize + hang;

				var cellSize = Frame.Size;

				TextField = new UITextField (new RectangleF (
					fieldXPosition, (cellSize.Height - h)/2, 
					cellSize.Width - fieldXPosition - 12, h)) {

					Font = FieldFont,
					Placeholder = field.Placeholder,
					Text = field.Value,
					TextColor = FieldColor,
					AutoresizingMask = UIViewAutoresizing.FlexibleWidth,

					SecureTextEntry = (field.FieldType == FormAuthenticatorFieldType.Password),

					KeyboardType = (field.FieldType == FormAuthenticatorFieldType.Email) ?
						UIKeyboardType.EmailAddress :
						UIKeyboardType.Default,

					AutocorrectionType = (field.FieldType == FormAuthenticatorFieldType.PlainText) ?
						UITextAutocorrectionType.Yes :
						UITextAutocorrectionType.No,
					
					AutocapitalizationType = UITextAutocapitalizationType.None,

					ShouldReturn = delegate {
						handleReturn ();
						return false;
					},
				};
				TextField.EditingDidEnd += delegate {
					field.Value = TextField.Text;
				};

				ContentView.AddSubview (TextField);
			}
		}

		class FormDataSource : UITableViewDataSource
		{
			FormAuthenticatorController controller;

			public FormDataSource (FormAuthenticatorController controller)
			{
				this.controller = controller;
			}

			public override int NumberOfSections (UITableView tableView)
			{
				return 2 + (controller.authenticator.Service.CreateAccountLink != null ? 1 : 0);
			}

			public override int RowsInSection (UITableView tableView, int section)
			{
				if (section == 0) {
					return controller.authenticator.Fields.Count;
				}
				else {
					return 1;
				}
			}

			FieldCell[] fieldCells = null;

			public void SelectNext ()
			{
				for (var i = 0; i < controller.authenticator.Fields.Count; i++) {
					if (fieldCells[i].TextField.IsFirstResponder) {
						if (i + 1 < fieldCells.Length) {
							fieldCells[i+1].TextField.BecomeFirstResponder ();
							return;
						}
						else {
							fieldCells[i].TextField.ResignFirstResponder ();
							controller.HandleSubmit ();
							return;
						}
					}
				}
			}

			public void ResignFirstResponder ()
			{
				foreach (var cell in fieldCells) {
					cell.TextField.ResignFirstResponder ();
				}
			}

			public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
			{
				if (indexPath.Section == 0) {
					if (fieldCells == null) {
						var fieldXPosition = controller
							.authenticator
							.Fields
							.Select (f => tableView.StringSize (f.Title, FieldCell.LabelFont).Width)
							.Max ();
						fieldXPosition += 36;

						fieldCells = controller
							.authenticator
							.Fields
							.Select (f => new FieldCell (f, fieldXPosition, SelectNext))
							.ToArray ();
					}

					return fieldCells[indexPath.Row];
				}
				else if (indexPath.Section == 1) {
					var cell = tableView.DequeueReusableCell ("SignIn");
					if (cell == null) {
						cell = new UITableViewCell (UITableViewCellStyle.Default, "SignIn");
						cell.TextLabel.TextAlignment = UITextAlignment.Center;
					}

					cell.TextLabel.Text = NSBundle.MainBundle.LocalizedString ("Sign In", "Sign In button title");

					return cell;
				}
				else {
					var cell = tableView.DequeueReusableCell ("CreateAccount");
					if (cell == null) {
						cell = new UITableViewCell (UITableViewCellStyle.Default, "CreateAccount");
						cell.TextLabel.TextAlignment = UITextAlignment.Center;
					}

					cell.TextLabel.Text = NSBundle.MainBundle.LocalizedString ("Create Account", "Create Account button title");

					return cell;
				}
			}
		}
	}
}
