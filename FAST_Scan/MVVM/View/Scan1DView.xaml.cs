using FAST_Scan.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using static FAST_Scan.Core.Scan;

namespace FAST_Scan.MVVM.View
{
    /// <summary>
    /// Interação lógica para Scan1DView.xam
    /// </summary>
    public partial class Scan1DView : UserControl
    {
        
        Scan scan;
        StatusMessage statusMessage;
        ScanAnalysis scanAnalysis;
        Scan.Axis axis; //guarda qual o eixo será realizado o scan

        public Scan1DView()
        {
            InitializeComponent();
            xRB.IsChecked = true;
            //evento para todas as textbox -> ir para proxima ao apertar enter
            EventManager.RegisterClassHandler(typeof(TextBox), TextBox.KeyDownEvent, new KeyEventHandler(TextBox_KeyDown));

            //adiciona elementos a combobox
            PulsePolarityCB.Items.Add("Negative");
            PulsePolarityCB.Items.Add("Positive");
            PulsePolarityCB.SelectedItem = null;

            digitizerSamplesTB.Text = "100";

            statusMessage = new StatusMessage();
            DataContext = statusMessage;

            // Escutar alterações na propriedade "Log"
            statusMessage.PropertyChanged += StatusMessage_PropertyChanged;

            StopScanButton.IsEnabled = false;
        }

        private void StatusMessage_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Log")
            {
                // Garantir que o scroll ocorra na UI thread
                Dispatcher.Invoke(() => StatusTextBox.ScrollToEnd());
            }
        }

        private async void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            Scan.ErrorStatus configError;
            Scan.ErrorStatus homeError;
            Scan.Axis homeAxis;
            int homeAxisValue = 0;

            if (xRB.IsChecked == true || xSetCB.IsChecked == true) { homeAxisValue += (int)Scan.Axis.X; }
            if (yRB.IsChecked == true || ySetCB.IsChecked == true) { homeAxisValue += (int)Scan.Axis.Y; }
            if (zRB.IsChecked == true || zSetCB.IsChecked == true) { homeAxisValue += (int)Scan.Axis.Z; }

            homeAxis = (Scan.Axis)homeAxisValue;

            scan = new Scan(statusMessage, Scan.ScanType.SCAN_2D, out configError);
            if (configError == Scan.ErrorStatus.CONFIGURE_DIGITIZER_FAIL)
            {
                MessageBox.Show("Unable to configure digitizer.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                scan = null;
                return;
            }
            else if (configError == Scan.ErrorStatus.CONFIGURE_SERVO_FAIL)
            {
                MessageBox.Show("Unable to configure Servo(s).", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                scan = null;
                return;
            }
            else if (configError == Scan.ErrorStatus.OK)
            {
                StartScanButton.IsEnabled = false;
                StopScanButton.IsEnabled = false;

                homeError = await Task.Run(() => scan.Home(homeAxis));

                if (homeError == Scan.ErrorStatus.OK)
                {
                    scan.Close();
                    scan = null;
                }
                else if (homeError == Scan.ErrorStatus.UNABLE_TO_HOME)
                {
                    scan.Close();
                    scan = null;
                    MessageBox.Show("Unable to Home Servo(s). ", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        //buttons
        private async void StartScanButton_Click(object sender, RoutedEventArgs e)
        {
            Scan.ErrorStatus errorScan;
            Scan.ErrorStatus configError;

            MessageBoxResult messageBoxResult;
            string dir;

            //verifica status de homing
            if (HommingStateManager.ServoXHomed == false || HommingStateManager.ServoYHomed == false)
            {
                messageBoxResult = MessageBox.Show("DO NOT START SCAN IF SERVO MOTORS ARE NOT HOMED!!!\nCurrent Homming Status: NOT HOMED.\nDo you want to change Homing Status to: HOMED?", "ERROR", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    HommingStateManager.SetIsHomed(HommingStateManager.Servo.X, true);
                    statusMessage.CreateStatusMessage("ServoX Homming Status Updated: HOMED");
                    HommingStateManager.SetIsHomed(HommingStateManager.Servo.Y, true);
                    statusMessage.CreateStatusMessage("ServoY Homming Status Updated: HOMED");
                }
                else if (messageBoxResult == MessageBoxResult.No)
                {
                    statusMessage.CreateStatusMessage("Make sure devices are Homed before Scan starts!");
                    return;
                }
            }

            scan = new Scan(statusMessage, Scan.ScanType.SCAN_2D, out configError);
            if (configError == Scan.ErrorStatus.CONFIGURE_DIGITIZER_FAIL)
            {
                MessageBox.Show("Unable to configure digitizer.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                scan = null;
                return;
            }
            else if (configError == Scan.ErrorStatus.CONFIGURE_SERVO_FAIL)
            {
                MessageBox.Show("Unable to configure Servo(s).", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                scan = null;
                return;
            }
            else if (configError == Scan.ErrorStatus.OK)
            {
                //confere se diretorio existe
                try
                {
                    dir = System.IO.Path.GetDirectoryName(saveFileTB.Text);
                    if (Directory.Exists(dir))
                    {
                        scan.setOutputFilePath(saveFileTB.Text);
                    }
                    else
                    {
                        MessageBox.Show("Save path invalid.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                        scan.Close();
                        scan = null;
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show("Save path invalid.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    scan.Close();
                    scan = null;
                    return;
                }

                //Verifica Erro em Digitizer Samples 
                errorScan = scan.setDigitizerSamplesValue(digitizerSamplesTB.Text);
                if (returnErrorConfigStatus(errorScan, "Digitizer Samples") == true)
                {
                    return;
                }

                //Verifica erro no Pulse Polarity Combobox
                if (PulsePolarityCB.SelectedItem == null)
                {
                    scan.Close();
                    MessageBox.Show("Invalid input on Pulse Polarity Configuration", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    errorScan = scan.setPulsePolarity(PulsePolarityCB.SelectedValue.ToString());
                }
                if (returnErrorConfigStatus(errorScan, "Pulse Polarity Configuration") == true)
                {
                    return;
                }

                StopScanButton.IsEnabled = true;
                StartScanButton.IsEnabled = false;

                //desabilita objetos após inicio do scan
                PulsePolarityCB.IsEnabled = false;
                StartTB.IsEnabled = false;
                StopTB.IsEnabled = false;
                StepsTB.IsEnabled = false;
                xPositionTB.IsEnabled = false;
                yPositionTB.IsEnabled = false;
                zPositionTB.IsEnabled = false;
                xSetCB.IsEnabled = false;
                ySetCB.IsEnabled = false;
                zSetCB.IsEnabled = false;
                saveFileTB.IsEnabled = false;
                BrowseButton.IsEnabled = false;
                ClearParametersButton.IsEnabled = false;

                //notifica globalmente que o scan está rodando
                ScanStateManager.SetScanRunning(true);

                StatusTextBox.AppendText("Iniciando scan...\n");

                await Task.Run(() => scan.Execute());

                FinishScan();
                StatusTextBox.AppendText("Scan Finalizado...\n");

                try
                {
                    if (GenerateImageCB.IsChecked == true)
                    {
                        scanAnalysis = new ScanAnalysis(statusMessage);
                        scanAnalysis.Generate2DScanMap(saveFileTB.Text);
                    }
                }
                catch
                {
                    statusMessage.CreateStatusMessage("Unable to run python script to generate Image");
                }

            }
            else
            {
                MessageBox.Show("Unexpected Error", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        private void StopScanButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Scan in progress. Are you sure you want to stop it?", "STOP SCAN", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                FinishScan();
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.Filter = "Text Files | *.txt";
            bool? success = fileDialog.ShowDialog();
            if (success == true)
            {
                string path = fileDialog.FileName;
                saveFileTB.Text = path;
            }
            else
            {
                //não selecionou nada
            }
        }

        //restrict text box content
        private void NumbersOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9+,]").IsMatch(e.Text);
        }
        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9+]").IsMatch(e.Text);
        }

        //Ao pressionar enter, passa para o próximo objeto
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                (sender as TextBox)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private bool returnErrorConfigStatus(Scan.ErrorStatus errorStatus, string str_errorPlace)
        {
            if (errorStatus == Scan.ErrorStatus.OK)
            {
                return false;
            }
            else if (errorStatus == Scan.ErrorStatus.INVALID_INPUT)
            {
                MessageBox.Show("Invalid input on " + str_errorPlace, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (errorStatus == Scan.ErrorStatus.VALUE_OUT_OF_RANGE)
            {
                MessageBox.Show("Input out of range on " + str_errorPlace, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show("Error on " + str_errorPlace, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            scan.Close();
            scan = null;
            return true;
        }

        private void FinishScan()
        {
            StatusTextBox.AppendText("Scan parado.\n");

            scan.Close();

            //notifica globalmente que o scan parou
            ScanStateManager.SetScanRunning(false);

            StopScanButton.IsEnabled = false;
            StartScanButton.IsEnabled = true;

            //habilita os objetos após a interrupção do scan
            PulsePolarityCB.IsEnabled = true;
            StartTB.IsEnabled = true;
            StopTB.IsEnabled = true;
            StepsTB.IsEnabled = true;
            
            if(axis != Scan.Axis.X)
            {
                xSetCB.IsEnabled = true;
            }
            if (axis != Scan.Axis.Y)
            {
                ySetCB.IsEnabled = true;
            }
            if (axis != Scan.Axis.Z)
            {
                ySetCB.IsEnabled = true;
            }

            saveFileTB.IsEnabled = true;
            BrowseButton.IsEnabled = true;
            ClearParametersButton.IsEnabled = true;


            //StatusTextBox.Text = string.Empty;
        }

        private void ClearTerminalButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBox.Text = string.Empty;
            //scanAnalysis = new ScanAnalysis(statusMessage);
            //scanAnalysis.Generate2DScanMap(saveFileTB.Text);
        }

        private void ClearParametersButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Are you sure you want to clear Input Parameters?", "CONFIRMATION", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                StartTB.Text = string.Empty;
                StopTB.Text = string.Empty;
                StepsTB.Text = string.Empty;

                xPositionTB.Text = string.Empty;
                yPositionTB.Text = string.Empty;
                zPositionTB.Text = string.Empty;

                saveFileTB.Text = string.Empty;
                PulsePolarityCB.SelectedItem = null;
            }
            if (result == MessageBoxResult.Cancel || result == MessageBoxResult.No)
            {
                return;
            }
        }

        //Radio Buttons para selecionar eixo de scan
        private void xRB_Checked(object sender, RoutedEventArgs e)
        {
            axis = Scan.Axis.X;
            xSetCB.IsEnabled = false;
            ySetCB.IsEnabled = true;
            zSetCB.IsEnabled = true;
            xSetCB.IsChecked = false;
        }

        private void yRB_Checked(object sender, RoutedEventArgs e)
        {
            axis = Scan.Axis.Y;
            ySetCB.IsEnabled = false;
            xSetCB.IsEnabled = true;
            zSetCB.IsEnabled = true;
            ySetCB.IsChecked = false;
        }

        private void zRB_Checked(object sender, RoutedEventArgs e)
        {
            axis = Scan.Axis.Z;
            zSetCB.IsEnabled = false;
            xSetCB.IsEnabled = true;
            ySetCB.IsEnabled = true;
            zSetCB.IsChecked = false;
        }
    }
}
