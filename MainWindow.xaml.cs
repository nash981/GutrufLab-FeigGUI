using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FEDM;
using Microsoft.Win32;
using static FEDM.Hm;
using FEDM.TagHandler;
using MLApp;
using MathWorks.MATLAB.Engine;
using MathWorks.MATLAB.Types;
using FEDM.Utility;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
//using LiveChartsCore;
//using LiveChartsCore.Defaults;
//using LiveChartsCore.SkiaSharpView;
//using LiveChartsCore.SkiaSharpView.Painting;
//using SkiaSharp;

namespace WpfApp2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ReaderModule reader = new ReaderModule(RequestMode.UniDirectional);
        ulong tagCount = 0;

        string consoleLog = "Console:\n";
        string extractedData = "";
        string tagInvdata = "";
        ThIso15693_STM_ST25DVxxK currentTag;
        private CancellationTokenSource cancellationTokenSource = null;
        private Task eventLoopTask;
        public ObservableCollection<string> tagListItems { get; set; }
        private Thread workerThread;
        private bool isRunning;


        public MainWindow()
        {   
            InitializeComponent();
        }

      
        // Partially DONE
        private void LRun_XML_Config(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Set properties for the OpenFileDialog
            openFileDialog.Title = "Select a File";
            openFileDialog.Filter = "All Files (*.*)|*.*";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Show the file dialog and get the selected file path
            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;

                // You can now use the selected file path in your application.
                // For example, you can display it in a TextBox or perform any desired operation.
                // Here, we'll display it in a MessageBox.
                MessageBox.Show($"Selected File: {selectedFilePath}", "File Selected");

                // TODO: Understand the XML file and run the configuration
                int state = reader.config().transferXmlFileToReaderCfg(selectedFilePath);
                if (state != ErrorCode.Ok) { /* Add error-handling... */ }
            }
        }

            
        // DONE
        private void readerInfo()
        {
            string data = "";
            // Output the deviceId
            data += $"{FEDM.DateTime.currentDateTime().toString()} >> deviceId: {reader.info().deviceId()}\n";
            data += $"{FEDM.DateTime.currentDateTime().toString()} >> deviceId: {reader.info().deviceIdToHexString()}\n";

            // Output the readerType
            data += $"{FEDM.DateTime.currentDateTime().toString()} >> readerType: {reader.info().readerType()}\n";
            data += $"{FEDM.DateTime.currentDateTime().toString()} >> readerType: {reader.info().readerTypeToString()}\n";

            // Output ACC FW Infos
            if (reader.info().accFw().isValid())
            {
                data += $"{FEDM.DateTime.currentDateTime().toString()} >> accFw: {reader.info().accFw().toReport()}\n";
            }

            // Output Writeable CFGs
            if (reader.info().wrCfgInfo().isValid())
            {
                data += $"{FEDM.DateTime.currentDateTime().toString()} >> wrCfgInfo: {reader.info().wrCfgInfo().toReport()}\n";
            }

            RCDataWindow.Text = data;

        }

        // Event Handlers for various buttons
        // ReaderConnect
        // DONE
        private void Reader_detect(object sender, RoutedEventArgs e)
        {
            try
            {
                // Scan connected USB Reader
                int scan = UsbManager.startDiscover();

                if (scan > 0)
                {
                    consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> Scan USB-Devices: {scan} FEIG Reader\n";
                }
                else
                {
                    consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> No FEIG Reader found.\n";
                    Console.Text = consoleLog;
                    return;
                }

                // Create Connector Object (USB) by using the UsbManager
                Connector usbConnector = UsbManager.popDiscover().connector();

                UsbManager.stopDiscover();

                

                // Connect Reader
                consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> Start connection with Reader: {usbConnector.usbDeviceId()}...\n";
                reader.connect(usbConnector);

                // if reader not in Reader List then add to reader list
                ReaderGrid.Items.Add(reader.info().deviceId());

                // Error handling
                if (reader.lastError() != ErrorCode.Ok)
                {
                    consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> Error while Connecting: {reader.lastError()}";
                    consoleLog += reader.lastErrorText();
                    Console.Text = consoleLog;
                    return;
                }

                // Output ReaderType
                consoleLog += $"{FEDM.DateTime.currentDateTime().toString()}>> Reader {reader.info().readerTypeToString()} connected.\n";
                readerInfo();

            }
            catch(Exception err)
            {
                consoleLog += err.Message;
            }
            Console.Text = consoleLog;
        }

        // CLUELESS
        private void ExistingConfig(object sender, RoutedEventArgs e)
        {

        }

        // CLUELESS
        private void QuickStart(object sender, RoutedEventArgs e)
        {

        }
        
        // Tag Inventory Page
        private void LoadTag(object sender, RoutedEventArgs e)
        {
            reader.hm().setUsageMode(UsageMode.UseTable);

            // Inventory without parameter use the default parameter:
            //         all = true
            //         Inventory Mode = 0x00

            int state = reader.hm().inventory();
            consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> inventory: {reader.lastErrorStatusText()}\n";
            if (state != ErrorCode.Ok) { /* Add error-handling... */
                consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> Error: Status Code {state}\n";
                Console.Text = consoleLog;
                return;
            }

            // Number of read tags
            tagCount = reader.hm().itemCount();
            consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> No. of tags: {tagCount}\n";

            // Create TagItem for each tag and output IDD
            for (ulong itemIndex = 0; itemIndex < tagCount; itemIndex++)
            {
                // Create TagItem
                TagItem tagItem = reader.hm().tagItem((uint)itemIndex);
                if (tagItem == null) { /* Add error handling */
                    consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >>Error: tag table empty\n";
                    Console.Text = consoleLog;
                    return; 
                }

                // Output IDD of TagItem
                String iddString = tagItem.iddToHexString();

                if (tagReadStatus(iddString)){
                    consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> IDD: {iddString} already detected\n";
                }
                else
                {
                    // Add the tags to item List in Tag Inventory Window
                    TagList.Items.Add(iddString);
                    consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> IDD: {iddString} detected and added\n";
                    // Add tags to the dropdown in extract data
                    tagList.Items.Add(iddString);
                }
            }
            if(tagList.Items.Count != 0)
            {
                tagList.SelectedIndex = 0;
            }
            Console.Text = consoleLog;


            // TODO: Populate the tagList with Tag IDs
        }
        private bool tagReadStatus(string newTag)
        {
            foreach (var item in tagList.Items)
            {
                // Assuming yourComboBox's items are strings.
                if (item.ToString() == newTag)
                {
                    return true;
                }
            }
            return false;
        }
        private void printTagData(TagItem tag)
        {
            tagInvdata = "";
            int state;
            ThBase th = reader.hm().createTagHandler(tag);
            if (th == null) { /* Add error handling */
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> Error: Failed to create tag handler"; 
                TagDetails.Text = tagInvdata;
                return; 
            }
            
            if (th is ThIso15693_STM_ST25DVxxK)
            {
                ThIso15693_STM_ST25DVxxK thIso15693 = (ThIso15693_STM_ST25DVxxK)th;
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> Tag {thIso15693.iddToHexString()} is {thIso15693.transponderName()}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> manufacturerName: {thIso15693.manufacturerName()}\n";


                // Add actions with tags here:

                // *******************
                // Get UID
                // *******************

                string idd = thIso15693.iddToHexString();
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> IDD: {idd}\n";

                ThIso15693_STM_ST25DVxxK.SystemInfo systemInfo = new ThIso15693_STM_ST25DVxxK.SystemInfo();
                bool isExtended = false; //If true then return the extended system information
                state = thIso15693.getSystemInformation(systemInfo, isExtended);
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> getSystemInformation: {ErrorCode.toString(state)}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> systemInfo.afi: {systemInfo.afi()}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> systemInfo.blockCount: {systemInfo.blockCount()}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> systemInfo.blockSize: {systemInfo.blockSize()}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> systemInfo.dsfId: {systemInfo.dsfId()}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> systemInfo.icReference: {systemInfo.icReference()}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> systemInfo.idd: {systemInfo.iddToHexString()}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> systemInfo.infoFlags: {systemInfo.infoFlags()}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> systemInfo.commandList: {HexConvert.toHexString(systemInfo.commandList())}\n";
                tagInvdata += $"{FEDM.DateTime.currentDateTime().toString()} >> systemInfo.cryptoSuiteIds: {HexConvert.toHexString(systemInfo.cryptoSuiteIds())}\n\n\n";
                TagDetails.Text = tagInvdata;
            }
        }
        private void ListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Check if an item is selected
            if (TagList.SelectedItem != null)
            {
                // Convert the selected item to a string (assuming the items are strings)
                string selectedItem = TagList.SelectedItem.ToString();

                // find the tag and print the info

                for (ulong itemIndex = 0; itemIndex < tagCount; itemIndex++)
                {
                    // Create TagItem
                    TagItem tagItem = reader.hm().tagItem((uint)itemIndex);

                    // Perform actions based on the selected item
                    if (selectedItem == tagItem.iddToHexString())
                    {
                        // Print data and break
                        tagInvdata = "Tag Selected:" + selectedItem;
                        printTagData(tagItem);
                    }
                   
                }
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Check if an item is selected
            if (tagList.SelectedItem != null)
            {
                // Get the selected item
                string selectedItem = (string)tagList.SelectedItem;

                // Perform actions based on the selected item
                for (ulong itemIndex = 0; itemIndex < tagCount; itemIndex++)
                {
                    // Create TagItem
                    TagItem tagItem = reader.hm().tagItem((uint)itemIndex);

                    // Perform actions based on the selected item
                    if (selectedItem == tagItem.iddToHexString())
                    {
                        // TODO: Update it so that current TAG is a th object
                        ThBase th = reader.hm().createTagHandler(tagItem);
                        if (th is ThIso15693_STM_ST25DVxxK)
                        {
                            currentTag = (ThIso15693_STM_ST25DVxxK)th;
                        }
                        else
                        {
                            MessageBox.Show($"Wrong Transponder");
                        }
                    }
                }
            }
        }
        private void ComboBox_SelectionChanged()
        {
            // Check if an item is selected
            if (tagList.SelectedItem != null)
            {
                // Get the selected item
                string selectedItem = (string)tagList.SelectedItem;

                // Perform actions based on the selected item
                for (ulong itemIndex = 0; itemIndex < tagCount; itemIndex++)
                {
                    // Create TagItem
                    TagItem tagItem = reader.hm().tagItem((uint)itemIndex);

                    // Perform actions based on the selected item
                    if (selectedItem == tagItem.iddToHexString())
                    {
                        // TODO: Update it so that current TAG is a th object
                        ThBase th = reader.hm().createTagHandler(tagItem);
                        if (th is ThIso15693_STM_ST25DVxxK)
                        {
                            currentTag = (ThIso15693_STM_ST25DVxxK)th;
                        }
                        else
                        {
                            MessageBox.Show($"Wrong Transponder");
                        }
                    }
                }
            }
        }
        private void ComboBox_SelectionChangedDispatched()
        {
            // Use Dispatcher to update UI on the UI thread
            Dispatcher.Invoke(() =>
            {
                // Check if an item is selected
                if (tagList.SelectedItem != null)
                {
                    // Get the selected item
                    string selectedItem = (string)tagList.SelectedItem;

                    // Perform actions based on the selected item
                    for (ulong itemIndex = 0; itemIndex < tagCount; itemIndex++)
                    {
                        // Create TagItem
                        TagItem tagItem = reader.hm().tagItem((uint)itemIndex);

                        // Perform actions based on the selected item
                        if (selectedItem == tagItem.iddToHexString())
                        {
                            // TODO: Update it so that current TAG is a th object
                            ThBase th = reader.hm().createTagHandler(tagItem);
                            if (th is ThIso15693_STM_ST25DVxxK)
                            {
                                currentTag = (ThIso15693_STM_ST25DVxxK)th;
                            }
                            else
                            {
                                MessageBox.Show($"Wrong Transponder");
                            }
                        }
                    }
                }
            });
        }


        // Extract Data
        private void write(object sender, RoutedEventArgs e)
        {

        }

        private void pltGraph(object sender, RoutedEventArgs e)
        {
            // Create a MATLAB instance
            MLApp.MLApp matlab = new MLApp.MLApp();

            // Specify the path to your .m file
            string scriptPath = @"C:\Users\ual-laptop\Downloads\PlottingFeigData_Charlie_Femur_AACC_Rock_11_23.m";

            // Execute the script
            matlab.Execute(@"cd 'C:\Users\ual-laptop\Downloads'");
            matlab.Execute($"run('{scriptPath}')");

        }

        private void read(object sender, RoutedEventArgs e)
        {   
            ComboBox_SelectionChanged();
            int state;
            ulong firstDataBlock = ulong.Parse(blkAddress.Text);
            // Limit Block Address to 64
            ulong noOfDataBlocks = ulong.Parse(blkCount.Text);
            ulong blockSize = 4;
            ulong msgSize = 64;
            DataBuffer data = new DataBuffer();

            state = currentTag.readMBMsg(0x00,255,data);
            extractedData += $"{FEDM.DateTime.currentDateTime().toString()} >> readMultipleBlocks: {reader.lastErrorStatusText()}\n";
            consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> readMultipleBlocks: {reader.lastErrorStatusText()}\n";
            if (state != ErrorCode.Ok) { /* Add error-handling... */ }

            extractedData += $"{FEDM.DateTime.currentDateTime().toString()} >> Read Data: {data.toHexString(" ")}\n\n";
            consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> Read Data: {data.toHexString(" ")}\n\n";
            data.Dispose();
            Console.Text = consoleLog;
            //ExtractedDataDeets.Text = extractedData;
        }

        // For threaded REad
        private void read()
        {

            ComboBox_SelectionChangedDispatched();
            Dispatcher.Invoke(() =>
            {
                int state;
                ulong firstDataBlock = ulong.Parse(blkAddress.Text);
                // Limit Block Address to 64
                ulong noOfDataBlocks = ulong.Parse(blkCount.Text);
                ulong blockSize = 4;
                DataBuffer data = new DataBuffer();

                state = currentTag.readMBMsg(0x00, 0x00, data);
                extractedData += $"{FEDM.DateTime.currentDateTime().toString()} >> readMultipleBlocks: {reader.lastErrorStatusText()}\n";
                consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> readMultipleBlocks: {reader.lastErrorStatusText()}\n";
                if (state != ErrorCode.Ok) { /* Add error-handling... */ }

                extractedData += $"{FEDM.DateTime.currentDateTime().toString()} >> Read Data: {data.toHexString(" ")}\n\n";
                consoleLog += $"{FEDM.DateTime.currentDateTime().toString()} >> Read Data: {data.toHexString(" ")}\n\n";
                data.Dispose();
                Console.Text = consoleLog;
            });

        }
        // Startup and Ending Code

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            MessageBoxResult result = MessageBox.Show("Do you want to close the application?", "Confirm Close", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true; // Cancel the window closing event
            }
            // Write the remaining output data in the file

            // Destroy the tag objects

            // Write the entire console output to a writefile

            // Close the reader
            // Disconnect Reader
            reader.disconnect();

            // Console Log output
            string path = @"C:\Users\ual-laptop\Documents";

            File.WriteAllText(@"C:\Users\ual-laptop\Documents\logOutput.txt",consoleLog);
            // Data Output
            File.WriteAllText(@"C:\Users\ual-laptop\Documents\data.txt", extractedData);


        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            /// Update this during MVC update

            // Setting up the List for list updates

//            tagListItems = new ObservableCollection<string>();
//            TagList.ItemsSource = tagListItems;
//            tagList.ItemsSource
        }
        /* private async void StartButton_Click(object sender, RoutedEventArgs e)
         {

             // Check if the event loop is already running
             if (eventLoopTask != null && !eventLoopTask.IsCompleted)
             {
                 consoleLog += "Bruhhhh";
                 return;
             }


             cancellationTokenSource = new CancellationTokenSource();

             // Start the event loop on a separate thread
             eventLoopTask = Task.Run(() => EventLoop(cancellationTokenSource.Token));

             // You can perform other setup or operations here
         }

         private void StopButton_Click(object sender, RoutedEventArgs e)
         {
             if (cancellationTokenSource != null)
             {
                 cancellationTokenSource.Cancel();
                 cancellationTokenSource = null;
                 // You can perform cleanup or other operations here
             }
         }

         private void EventLoop(CancellationToken cancellationToken)
         {
             while (!cancellationToken.IsCancellationRequested)
             {
                 //();

                 // Simulate work (remove this in your actual application)
                 //Thread.Sleep(3000);


                 Application.Current.Dispatcher.Invoke(() =>
                 {
                     // Perform any UI-related updates here

                 });
             }
         }*/

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning)
            {
                // Start your operation on a separate thread
                workerThread = new Thread(DoWork);
                workerThread.Start();
                isRunning = true;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                // Stop your operation
                isRunning = false;
                workerThread.Join(); // Wait for the thread to finish gracefully
            }
        }

        private void DoWork()
        {
            // Replace this with the code you want to run continuously
            while (isRunning)
            {
                // Gain the lock
                // Your continuous operation here
                read();

                // Release the lock

                // For example, you can print something to the console
                //Console.WriteLine("Working...");

                // Sleep for a short duration to simulate work
                Thread.Sleep(112);
            }
        }

        private void Button_Click (object sender, RoutedEventArgs e)
        {

        }

    }
}
