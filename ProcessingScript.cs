using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FEIGWPFSoln
{
    class ProcessingScript
    {
        public void batchProcess(string filename)
        {
            // %% Import data from text file
            string filePath = filename;

            // Check if the file exists
            if (File.Exists(filePath))
            {
                // Read all lines from the file
                string[] lines = File.ReadAllLines(filePath);


                // Remove all lines containting ">>" from the file
                lines = lines.Where(x => !x.Contains(">>")).ToArray();
                // Remove all the RF warnings from the file
                lines = lines.Where(x => !x.Contains("Reader: RF-Warning")).ToArray();
                // Remove no tag in field warnings from the file
                lines = lines.Where(x => !x.Contains("Reader: No Transponder in Reader Field")).ToArray();
                // Replace "  <<  " with a single space
                lines = lines.Select(x => x.Replace("  <<  ", " ")).ToArray();
                // Remove transponder validation section from the lines
                lines = lines.Select(x => x.Replace("   Transponder: Data False", "")).ToArray();
                // Remove all rows whose last column is not "A0"
                lines = lines.Where(x => x.Split(' ').Last() == "A0").ToArray();

                // Split every line into an array of data and store it in a separate list
                List<string[]> data = new List<string[]>();
                foreach (string line in lines)
                {
                    data.Add(line.Split(' '));
                }
                // Count the number of columns in the data
                int numCols = data[0].Length;

                List<string[]> dateTime = new List<string[]>();
                List<string[]> feigPreamble = new List<string[]>();
                List<string[]> mailboxData = new List<string[]>();
                List<string[]> crc = new List<string[]>();
                List<string[]> status = new List<string[]>();
                List<int[]> hexData = new List<int[]>();
                foreach (string[] line in data)
                {
                    // Column 0 and 1 are date and time 
                    dateTime.Add(line.Take(2).ToArray());
                    // Column 2:8 are the Feig preamble
                    feigPreamble.Add(line.Skip(2).Take(7).ToArray());
                    // Column 9:264 data
                    mailboxData.Add(line.Skip(9).Take(256).ToArray());
                    // Column 265:266 CRC
                    crc.Add(line.Skip(265).Take(2).ToArray());
                    // Column 267 status
                    status.Add(line.Skip(267).Take(1).ToArray());
                }
                // print the mailbox data
                // foreach (string[] line in mailboxData)
                // {
                //     Console.WriteLine(string.Join(" ", line));
                // }
                // TODO: Process date and time
                // TODO: Process the mailbox data line by line
                for (int i = 0; i < mailboxData.Count; i++)
                {
                    hexData.Add(ProcessHex(mailboxData[i]));
                }
                List<int[]> strainData = new List<int[]>();
                List<int[]> tempData = new List<int[]>();
                List<int[]> absTimestamp = new List<int[]>();
                List<int[]> relTimestamp = new List<int[]>();
                List<int[]> transCounter = new List<int[]>();
                foreach (int[] line in hexData)
                {
                    // total 128 columns
                    // Extract Temperature data second column
                    tempData.Add(line.Skip(1).Take(1).ToArray());
                    // Extract absolute timestamp third and fourth column
                    absTimestamp.Add(line.Skip(2).Take(2).ToArray());
                    // Extract Counters  last column
                    transCounter.Add(line.Skip(127).Take(1).ToArray());
                    // Process the Strain Data 5th to 125th column
                    strainData.Add(processStrain(line.Skip(4).Take(122).ToArray()));
                    // Process the relative timestamp 126th column
                    relTimestamp.Add(processRelativeTimestamp(line.Skip(4).Take(122).ToArray()));
                }


                // Conver the tempData into an integer array it is a two dimensional array with only one element each row lol
                int[] tempDataInt = new int[tempData.Count];
                for (int i = 0; i < tempData.Count; i++)
                {
                    tempDataInt[i] = tempData[i][0];
                }


                // // Convert temprature voltages to values in celcius
                double[] tempCelcius = ProcessTempC(tempDataInt);
                // print the temperature data
                foreach (double t in tempCelcius)
                {
                    Console.WriteLine(t);
                }

                // Convert the temperature to fahrenheit
                double[] tempFahrenheit = ProcessTempF(tempCelcius);
                // print the temperature data
                foreach (double t in tempFahrenheit)
                {
                    Console.WriteLine(t);
                }

                // print date
                foreach (string[] line in dateTime)
                {
                    Console.WriteLine(string.Join(" ", line));
                }

                // Write data to a csv file in following format
                // Date, Time, TemperatureF, TempC, AbsTstamp, strain, RelTstamp, Counter
                // Create a list of strings to store the data
                List<string> csvData = new List<string>();
                for (int i = 0; i < strainData.Count; i++)
                {
                    for (int j = 0; j < strainData[0].Length; j++)
                    {
                        csvData.Add(string.Join(",", dateTime[i][0], dateTime[i][1], tempFahrenheit[i], tempCelcius[i], absTimestamp[i][0], strainData[i][j], relTimestamp[i][j], transCounter[i][0]));
                    }
                }
                // Write the data to a csv file
                File.WriteAllLines("../9A8E_benchtop_temp.csv", csvData);
                Console.WriteLine("Data written to csv file");


            }
            else
            {
                Console.WriteLine("File does not exist.");
            }



            { 

                // % Convert strain ADC reading -> voltage -> strain
                // %% Format time to be continuous even if reset
                // % Search for moments when implant has restarted counting from zero
                // % Check Feig reader timestamp when the restarted time sample ocurred
                // % Take difference in ms from the timestamp just before the reset
                // % Do this for all resets to create a continuous running timestamp
                // %% Create running time array and remove duplicates
                // % Only keep timestamps which are unique
                // % Convert timestamps in ms to time in s
                // % Drop temp and strain samples which were from those duplicate data sets
                // % Take each column and append to the end of the previous column to make one
                // % continuous data set. Use the transverse so it instead takes each row
            }
        }
        public double[] ProcessTempF(double[] tempData)
        {
            // Convert temp
            double[] temp = new double[tempData.Length];
            for (int i = 0; i < tempData.Length; i++)
            {
                temp[i] = tempData[i] * 1.8 + 32;
            }
            return temp;
        }

        // Function to process temperature data from voltage to temperature
        public double[] ProcessTempC(int[] tempData)
        {
            // Convert temp
            double[] temp = new double[tempData.Length];
            for (int i = 0; i < tempData.Length; i++)
            {
                temp[i] = ((1.8 * tempData[i] / 16372) - 2.9761) / -0.0507;
            }
            return temp;
        }

        // Function to process hex data string into an array of hex values
        public int[] ProcessHex(string[] hexString)
        {
            // bunch two strings starting from the first string and convert them to hex and store them in a list
            List<int> hexList = new List<int>();
            for (int i = 0; i < hexString.Length; i += 2)
            {
                // Console.WriteLine(hexString[i] + hexString[i + 1]);
                hexList.Add(Convert.ToInt32(hexString[i] + hexString[i + 1], 16));
            }
            // // print the hex list
            // foreach (int hex in hexList)
            // {
            //     Console.WriteLine(hex);
            // }
            return hexList.ToArray();

        }
        public int[] processStrain(int[] strainData)
        {
            // Alternate bytes. Even bytes are strain data and odd bytes are relative timestamp
            List<int> strainList = new List<int>();
            for (int i = 0; i < strainData.Length; i += 2)
            {
                strainList.Add(strainData[i]);
            }

            return strainList.ToArray();
        }
        public int[] processRelativeTimestamp(int[] relTimestamp)
        {
            // Alternate bytes. Even bytes are strain data and odd bytes are relative timestamp
            List<int> timeStamp = new List<int>();
            for (int i = 1; i < relTimestamp.Length; i += 2)
            {
                timeStamp.Add(relTimestamp[i]);
            }

            return timeStamp.ToArray();
        }


    }
}
