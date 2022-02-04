﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using LinkOS.Plugin;
using LinkOS.Plugin.Abstractions;
using MvvmHelpers;
using MvvmHelpers.Commands;
using Xamarin.Forms;
using ZebraBluetoothSample.Dependencies;
using ZebraBluetoothSample.Models;
using ZebraBluetoothSample.Pages;
using ZXing.Net.Mobile.Forms;
using Command = MvvmHelpers.Commands.Command;

namespace ZebraBluetoothSample
{
    public partial class ZebraBluetoothSamplePage : ContentPage
    {
        #region Properties
        ZXingScannerPage scanPage;
        public delegate void PrinterSelectedHandler(IDiscoveredPrinter printer);
        public static event PrinterSelectedHandler OnPrinterSelected;
        ObservableCollection<IDiscoveredPrinter> printers = new ObservableCollection<IDiscoveredPrinter>();
        protected IDiscoveredPrinter ChoosenPrinter;
        public string barcodeText;
        public string codeText;
        public string itemNumberText;
        public string netWeightText;
        int count = 1;
        public ObservableRangeCollection<Barcode> Barcodes { get; set; }
        public AsyncCommand RefreshCommand { get; }
        public AsyncCommand<Barcode> PrintCommand { get; }
        public AsyncCommand<Barcode> RemoveCommand { get; }
        public AsyncCommand EmptyCommand { get; }

        IBarcodeService _barcodeService;
        #endregion


        public ZebraBluetoothSamplePage()
        {
            InitializeComponent();
            Barcodes = new ObservableRangeCollection<Barcode>();
            BindingContext = this;


            RefreshCommand = new AsyncCommand(Refresh);
            PrintCommand = new AsyncCommand<Barcode>(Print);
            EmptyCommand = new AsyncCommand(Empty);
            RemoveCommand = new AsyncCommand<Barcode>(Remove);
            _barcodeService = DependencyService.Get<IBarcodeService>();
            lstDevices.ItemsSource = printers;
            lstBarcodes.ItemSelected += LstBarcodes_ItemSelected; ;
            lstDevices.ItemSelected += LstDevices_ItemSelected; ;
            btnScan.Clicked += (sender, e) =>
            {
                IsBusy = true;
                loading.IsRunning = true;
                Task.Run(()=>{
                    StartBluetoothDiscovery();
                });
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (Barcodes.Count == 0)
                await RefreshCommand.ExecuteAsync();
        }

        void LstBarcodes_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            var ChoosenBarcode = e.SelectedItem as Barcode;
            barcodeText = ChoosenBarcode.Text;
            DisplayAlert("Barcode Info", ChoosenBarcode.Text, "OK");
        }

        void LstDevices_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            //Stop searching for bluetooth devices/printers
            DependencyService.Get<IPrinterDiscovery>().CancelDiscovery();

            //Object type for printers returned are DiscoveredPrinters, theres an additional type that says USB but is not the target of this project
            //We assign now the printer selected from the list.
            ChoosenPrinter = e.SelectedItem as IDiscoveredPrinter;
        }

        #region Printing
        async Task Print(Barcode barcode)
        {
            if (barcode == null)
                return;

            barcodeText = barcode.Text;
            codeText = barcode.Code;
            itemNumberText = barcode.ItemNumber;
            netWeightText = barcode.NetWeight;
            IConnection connection = null;
            try
            {
                connection = ChoosenPrinter.Connection;
                if (connection == null)
                {
                    await DisplayAlert("Print Error", "No Printer Connected", "OK");
                    return;
                }
                connection.Open();
                IZebraPrinter printer = ZebraPrinterFactory.Current.GetInstance(connection);
                if ((!CheckPrinterLanguage(connection)) || (!PreCheckPrinterStatus(printer)))
                {
                    return;
                }
                for(int i = 0; i < count; i++)
                {
                    sendZplBarcode(connection);
                }
                if ((connection != null) && (connection.IsConnected))
                    connection.Close();
            }
            catch (Exception ex)
            {
                // Connection Exceptions and issues are caught here
                Debug.WriteLine(ex.Message);
            }
            var id = await _barcodeService.GetBarcode(barcode.Id);
        }

        public ICommand PrintAllCommand => new Command(async () =>
        {
            var barcodes = await _barcodeService.GetBarcode();
            if (barcodes == null)
                return;
            IConnection connection = null;
            try
            {
                connection = ChoosenPrinter.Connection;
                if (connection == null)
                {
                    await DisplayAlert("Print Error", "No Printer Connected", "OK");
                    return;
                }
                connection.Open();
                IZebraPrinter printer = ZebraPrinterFactory.Current.GetInstance(connection);
                if ((!CheckPrinterLanguage(connection)) || (!PreCheckPrinterStatus(printer)))
                {
                    return;
                }
                foreach (var barcode in barcodes)
                {
                    barcodeText = barcode.Text;
                    codeText = barcode.Code;
                    itemNumberText = barcode.ItemNumber;
                    netWeightText = barcode.NetWeight;
                    for (int i = 0; i < count; i++)
                    {
                        sendZplBarcode(connection);
                    }
                }
                if ((connection != null) && (connection.IsConnected))
                    connection.Close();
            }
            catch (Exception ex)
            {
                // Connection Exceptions and issues are caught here
                Debug.WriteLine(ex.Message);
            }     
        });

        #endregion

        public ICommand LabelModeCommand => new Command(async () =>
        {
            IConnection connection = null;
            try
            {
                connection = ChoosenPrinter.Connection;
                if (connection == null)
                {
                    await DisplayAlert("Print Error", "No Printer Connected", "OK");
                    return;
                }
                connection.Open();
                IZebraPrinter printer = ZebraPrinterFactory.Current.GetInstance(connection);
                if ((!CheckPrinterLanguage(connection)) || (!PreCheckPrinterStatus(printer)))
                {
                    return;
                }

                sendZplLabelMode(connection);

                if ((connection != null) && (connection.IsConnected))
                    connection.Close();
            }
            catch (Exception ex)
            {
                // Connection Exceptions and issues are caught here
                Debug.WriteLine(ex.Message);
            }
        });


        #region Zebra methods/functions

        //Start searching for printers
        private void StartBluetoothDiscovery()
        {
            Debug.WriteLine("Discovering Bluetooth Printers");
            IDiscoveryEventHandler bthandler = DiscoveryHandlerFactory.Current.GetInstance();
            bthandler.OnDiscoveryError += DiscoveryHandler_OnDiscoveryError;
            bthandler.OnDiscoveryFinished += DiscoveryHandler_OnDiscoveryFinished;
            bthandler.OnFoundPrinter += DiscoveryHandler_OnFoundPrinter;
           
            System.Diagnostics.Debug.WriteLine("Starting Bluetooth Discovery");
            DependencyService.Get<IPrinterDiscovery>().FindBluetoothPrinters(bthandler);
        }


        private void DiscoveryHandler_OnFoundPrinter(object sender, IDiscoveredPrinter discoveredPrinter)
        {

            Debug.WriteLine("Found Printer:" + discoveredPrinter.ToString());
            Device.BeginInvokeOnMainThread(() => {
                lstDevices.BatchBegin();

                if (!printers.Contains(discoveredPrinter))
                {
                    printers.Add(discoveredPrinter);
                }
                lstDevices.BatchCommit();
            });
        }

        private void DiscoveryHandler_OnDiscoveryFinished(object sender)
        {
            Debug.WriteLine("Discovery Finished");
            Device.BeginInvokeOnMainThread(()=>{
                loading.IsRunning = false;
                IsBusy = false;
            });
        }

        private void DiscoveryHandler_OnDiscoveryError(object sender, string message)
        {
            Debug.WriteLine("On Discovery Error" );
            Debug.WriteLine(message);
        }

        //Connect and send to print
        private void PrintLineMode()
        {
            IConnection connection = null;
            try
            {

                connection = ChoosenPrinter.Connection;
                connection.Open();
                IZebraPrinter printer = ZebraPrinterFactory.Current.GetInstance(connection);
                if ((!CheckPrinterLanguage(connection)) || (!PreCheckPrinterStatus(printer)))
                {
                    
                    return;
                }
                sendZplBarcode(connection);
                if (PostPrintCheckStatus(printer)) {
                    Debug.WriteLine("Printing process is done");
                }
            }
            catch (Exception ex)
            {
                // Connection Exceptions and issues are caught here
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                connection.Open();
                if ((connection != null) && (connection.IsConnected))
                    connection.Close();
                
            }
        }

       
        //Format and construct the body of the printer string
        private void sendZplBarcode(IConnection printerConnection)
        {
            /*
             This routine is provided to you as an example of how to create a variable length label with user specified data.
             The basic flow of the example is as follows

                Header of the label with some variable data
                REMOVED TO TAKE THE EXAMPLE AS SIMPLE AS POSSIBLE Body of the label
                REMOVED TO TAKE THE EXAMPLE AS SIMPLE AS POSSIBLE     Loops thru user content and creates small line items of printed material
                REMOVED TO TAKE THE EXAMPLE AS SIMPLE AS POSSIBLE Footer of the label

             As you can see, there are some variables that the user provides in the header, body and footer, and this routine uses that to build up a proper ZPL string for printing.
             Using this same concept, you can create one label for your receipt header, one for the body and one for the footer. The body receipt will be duplicated as many items as there are in your variable data

             */

            String tmpHeader =
                    /*
                     Some basics of ZPL. Find more information here : http://www.zebra.com

                     ^XA indicates the beginning of a label
                     ^PW sets the width of the label (in dots)
                     ^MNN sets the printer in continuous mode (variable length receipts only make sense with variably sized labels)
                     ^LL sets the length of the label (we calculate this value at the end of the routine)
                     ^LH sets the reference axis for printing. 
                        You will notice we change this positioning of the 'Y' axis (length) as we build up the label. Once the positioning is changed, all new fields drawn on the label are rendered as if '0' is the new home position
                     ^FO sets the origin of the field relative to Label Home ^LH
                     ^A sets font information 
                     ^FD is a field description
                     ^GB is graphic boxes (or lines)
                     ^B sets barcode information
                     ^XZ indicates the end of a label
                     */

                    "^XA" +

                    "^FO10,30" + "\r\n" + "^A0,52,50" + "\r\n" + "^FD Nyrstar BHAS 9997^FS" + "\r\n" +

                    "^FO585,25" + "\r\n" + "^GB100,40,20" + "\r\n" + "^FS" + "\r\n" +

                    "^FO550,70" + "\r\n" + "^A0,25,25" + "\r\n" + "^FD Net Weight (kg)^FS" + "\r\n" +
                    "^FO600,30" + "\r\n" + "^A0,35,35" + "\r\n" + "^FR" + "\r\n"  + "^FD" + netWeightText + "^FS" + "\r\n" +

                    "^FO60,100" + "\r\n" + "^GB680,0,4" + "\r\n" + "^FS" + "\r\n" +
                    "^FO60,325" + "\r\n" + "^GB680,0,4" + "\r\n" + "^FS" + "\r\n" +

                    "^FO175,130" + "\r\n" + "^BCN,150,Y,N,N,A" + "\r\n" + "^FD" + barcodeText + "^FS" + "\r\n" +

                    //"^FO425,30" + "\r\n" + "^A0,N,25,25" + "\r\n" + "^FD{0}^FS" + "\r\n" 
                    "^FO100,340" + "\r\n" + "^A0,35,35" + "\r\n" + "^FD Code^FS" + "\r\n" +
                    "^FO200,340" + "\r\n" + "^A0,35,35" + "\r\n" + "^FD" + codeText + "^FS" + "\r\n" +

                    "^FO400,340" + "\r\n" + "^A0,35,35" + "\r\n" + "^FD Item No.^FS" + "\r\n" +
                    "^FO550,340" + "\r\n" + "^A0,35,35" + "\r\n" + "^FD" + itemNumberText + "^FS" + "\r\n" +

                    
                    "^XZ";

            

            DateTime date = DateTime.Now;
            string dateString = date.ToString("MMM dd, yyyy");

            string header = string.Format(tmpHeader, dateString);
            var t = new UTF8Encoding().GetBytes(header);
            printerConnection.Write(t);


        }

        private void sendZplLabelMode(IConnection printerConnection)
        {

            String tmpHeader =

                    "~JC" +
                    "^XA" +
                    "^JUS" +
                    "^XZ";

            var t = new UTF8Encoding().GetBytes(tmpHeader);
            printerConnection.Write(t);


        }

        //Check if the printer is not null
        //If it is null means we should select one first
        protected bool CheckPrinter()
        {
            if (ChoosenPrinter == null)
            {
                Debug.WriteLine("Please Select a printer");
                //SelectPrinter();
                return false;
            }
            return true;
        }


        //More info https://www.zebra.com/content/dam/zebra/manuals/en-us/software/zpl-zbi2-pm-en.pdf
        protected bool CheckPrinterLanguage(IConnection connection)
        {
            if (!connection.IsConnected)
                connection.Open();
            //  Check the current printer language
            byte[] response = connection.SendAndWaitForResponse(new UTF8Encoding().GetBytes("! U1 getvar \"device.languages\"\r\n"), 500, 100);
            string language = Encoding.UTF8.GetString(response, 0, response.Length);
            if (language.Contains("line_print"))
            {
                Debug.WriteLine("Switching printer to ZPL Control Language.", "Notification");
            }
            // printer is already in zpl mode
            else if (language.Contains("zpl"))
            {
                return true;
            }

            //  Set the printer command languege
            connection.Write(new UTF8Encoding().GetBytes("! U1 setvar \"device.languages\" \"zpl\"\r\n"));
            response = connection.SendAndWaitForResponse(new UTF8Encoding().GetBytes("! U1 getvar \"device.languages\"\r\n"), 500, 100);
            language = Encoding.UTF8.GetString(response, 0, response.Length);
            if (!language.Contains("zpl"))
            {
                Debug.WriteLine("Printer language not set. Not a ZPL printer.");
                return false;
            }
            return true;
        }


        //Before printing, check current printer status
        protected bool PreCheckPrinterStatus(IZebraPrinter printer)
        {
            // Check the printer status
            IPrinterStatus status = printer.CurrentStatus;
            if (!status.IsReadyToPrint)
            {
                Debug.WriteLine("Unable to print. Printer is " + status.Status);
                return false;
            }
            return true;
        }


        //Check what happens to the printer after print command was sent
        protected bool PostPrintCheckStatus(IZebraPrinter printer)
        {
            // Check the status again to verify print happened successfully
            IPrinterStatus status = printer.CurrentStatus;
            // Wait while the printer is printing
            while ((status.NumberOfFormatsInReceiveBuffer > 0) && (status.IsReadyToPrint))
            {
                status = printer.CurrentStatus;
            }
            // verify the print didn't have errors like running out of paper
            if (!status.IsReadyToPrint)
            {
                Debug.WriteLine("Error durring print. Printer is " + status.Status);
                return false;
            }
            return true;
        }

        #endregion

        private async void Button_Clicked(object sender, EventArgs e)
        {
            scanPage = new ZXingScannerPage(new ZXing.Mobile.MobileBarcodeScanningOptions { DelayBetweenContinuousScans = 3000 });
            scanPage.OnScanResult += (result) =>
            {
                Device.BeginInvokeOnMainThread(async () =>
                {
                    var text = result.Text;
                    if (text.Length != 16)
                    {
                        await Application.Current.MainPage.DisplayAlert("Barcode Error", "Scanned Barcode is invalid", "OK");
                        return;
                    }
                    await DisplayAlert("Scanned Barcode", result.Text, "OK");
                    await _barcodeService.AddBarcode(text);
                    await Refresh();
                });
            };

            await Navigation.PushAsync(scanPage);
        }
        async Task Empty()
        {
            var barcodes = await _barcodeService.GetBarcode();
            foreach (var barcode in barcodes)
            {
                await _barcodeService.RemoveBarcode(barcode.Id);
            }
            await Refresh();
        }

        async Task Remove(Barcode barcode)
        {
            await _barcodeService.RemoveBarcode(barcode.Id);
            await Refresh();
        }

        async Task Refresh()
        {
            IsBusy = true;

#if DEBUG
            await Task.Delay(500);
#endif

            Barcodes.Clear();

            var barcodes = await _barcodeService.GetBarcode();

            Barcodes.AddRange(barcodes);

            IsBusy = false;
        }

        private void AddButton_Clicked(object sender, EventArgs e)
        {
            count++;
            Copies.Text = $"Copies: {count}";
        }

        private void MinusButton_Clicked(object sender, EventArgs e)
        {
            if (count == 1)
                return;
            count--;
            Copies.Text = $"Copies: {count}";
        }
    }
}
