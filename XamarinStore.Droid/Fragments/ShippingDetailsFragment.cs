using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Views.InputMethods;
using System.Threading.Tasks;
using Xamarin.Geolocation;
using Android.Locations;

namespace XamarinStore
{
	public class ShippingDetailsFragment : Fragment
	{
		User user;
		AutoCompleteTextView state;
		AutoCompleteTextView country;
		EditText streetAddress1, city, postalCode;
		Geolocator locator;

		public ShippingDetailsFragment() : this(new User()) {}

		public ShippingDetailsFragment(User user)
		{
			this.user = user;
		}

		public override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			RetainInstance = true;
			SetHasOptionsMenu (true);
		}

		public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			var shippingDetailsView = inflater.Inflate(Resource.Layout.ShippingDetails, container, false);

			var placeOrder = shippingDetailsView.FindViewById<Button> (Resource.Id.PlaceOrder);
			var phone = shippingDetailsView.FindViewById<EditText> (Resource.Id.Phone);
			phone.Text = user.Phone;

			var firstName = shippingDetailsView.FindViewById<EditText> (Resource.Id.FirstName);
			firstName.Text = user.FirstName;

			var lastName = shippingDetailsView.FindViewById<EditText> (Resource.Id.LastName);
			lastName.Text = user.LastName;

			streetAddress1 = shippingDetailsView.FindViewById<EditText> (Resource.Id.StreetAddress1);
			streetAddress1.Text = user.Address;

			var streetAddress2 = shippingDetailsView.FindViewById<EditText> (Resource.Id.StreetAddress2);
			streetAddress2.Text = user.Address2;

			city = shippingDetailsView.FindViewById<EditText> (Resource.Id.City);
			city.Text = user.City;

			state = shippingDetailsView.FindViewById<AutoCompleteTextView> (Resource.Id.State);
			state.Text = user.State;

			postalCode = shippingDetailsView.FindViewById<EditText> (Resource.Id.PostalCode);
			postalCode.Text = user.ZipCode;

			country = shippingDetailsView.FindViewById<AutoCompleteTextView> (Resource.Id.Country);
			user.Country = string.IsNullOrEmpty (user.Country) ? "United States" : user.Country;
			country.Text = user.Country;
			country.ItemSelected += (object sender, AdapterView.ItemSelectedEventArgs e) => {
				LoadStates();
			};

			placeOrder.Click += async (sender, e) => {
				var entries = new EditText[] {
					phone, streetAddress1, streetAddress2, city, state, postalCode, country
				};
				foreach (var entry in entries)
					entry.Enabled = false;
				user.FirstName = firstName.Text;
				user.LastName = lastName.Text;
				user.Phone = phone.Text;
				user.Address = streetAddress1.Text;
				user.Address2 = streetAddress2.Text;
				user.City = city.Text;
				user.State = state.Text;
				user.ZipCode = postalCode.Text;
				user.Country = await WebService.Shared.GetCountryCode(country.Text);
				await ProcessOrder();
				foreach (var entry in entries)
					entry.Enabled = true;
			};
			LoadCountries ();
			LoadStates ();

			CheckGeoAvailable ();

			return 	shippingDetailsView;
		}

		public override void OnCreateOptionsMenu (IMenu menu, MenuInflater inflater)
		{
			inflater.Inflate (Resource.Menu.shipping, menu);
			base.OnCreateOptionsMenu (menu, inflater);
		}

		public override bool OnOptionsItemSelected (IMenuItem item)
		{
			switch (item.ItemId) {
			case Resource.Id.findlocation_menu_item:
				GetLocation ();
				return true;

			default:
				return base.OnOptionsItemSelected (item);
			}

		}

		async void LoadCountries()
		{
			var countries = await WebService.Shared.GetCountries ();
			country.Adapter = new ArrayAdapter(this.Activity, Android.Resource.Layout.SimpleDropDownItem1Line, countries.Select(x=> x.Name).ToList());
		}

		async void LoadStates()
		{
			var states = await WebService.Shared.GetStates (country.Text);
			state.Adapter = new ArrayAdapter(this.Activity, Android.Resource.Layout.SimpleDropDownItem1Line, states);
		}

		async Task ProcessOrder ()
		{	
			var isValid = await user.IsInformationValid ();
			if (!isValid.Item1) {
				Toast.MakeText (Activity, isValid.Item2, ToastLength.Long).Show ();
				return;
			}

			var progressDialog = ProgressDialog.Show(this.Activity, "Please wait...", "Placing Order", true);
			var result = await WebService.Shared.PlaceOrder (user);
			progressDialog.Hide ();
			progressDialog.Dismiss ();
			string message = result.Success ? "Your order has been placed!" : "Error: " + result.Message;
			Toast.MakeText (Activity, message, ToastLength.Long).Show ();
			if (!result.Success)
				return;
			var op = OrderPlaced;
			if (op != null)
				op ();
		}

		public Action OrderPlaced {get;set;}
	
	
		void CheckGeoAvailable()
		{
			locator = new Geolocator (this.Activity);
			if (locator.IsGeolocationAvailable) {
				AlertDialog.Builder dialog = new AlertDialog.Builder (this.Activity);
				dialog.SetTitle (Resource.String.app_name);
				dialog.SetMessage ("Do you like to fill address with your current location ?");
				dialog.SetCancelable (false);

				dialog.SetPositiveButton (Android.Resource.String.Yes, (object sender, DialogClickEventArgs e) => {
					GetLocation();
				});

				dialog.SetNegativeButton (Android.Resource.String.No, (object sender, DialogClickEventArgs e) => {
					((Dialog)sender).Dismiss();
				});

				dialog.Show ();
			}
		}

		async void GetLocation()
		{
			//search for Latitude and Longitude
			var progressDialog = ProgressDialog.Show(this.Activity, "Please wait...", "Finding Location", true);
			var t = await locator.GetPositionAsync (timeout: 10000);

			//search for address
			try {
				progressDialog.SetMessage ("Finding Address");
				var geo = new Geocoder (this.Activity);
				var addresses = await geo.GetFromLocationAsync (t.Latitude, t.Longitude, 1);
				geo.Dispose ();


				if (addresses.Any ()) {

					var address = addresses.First ();

					if (address != null) {
						streetAddress1.Text = String.Format ("{0} {1}", address.Thoroughfare, address.SubThoroughfare).Trim ();
						postalCode.Text = address.PostalCode;
						country.Text = address.CountryName;
						state.Text = address.AdminArea;
						city.Text = address.SubAdminArea;
					}

				} else {
					Toast.MakeText (Activity, "Sorry :(... Address not found", ToastLength.Long).Show ();
				}

			} catch (Exception) {

				Toast.MakeText (Activity, "We have a problem", ToastLength.Long).Show ();
			}



			progressDialog.Dismiss ();

		}
	}

}
