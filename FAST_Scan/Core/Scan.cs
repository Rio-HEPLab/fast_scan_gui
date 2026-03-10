#define TEST_MODE

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Thorlabs.MotionControl.DeviceManagerCLI;
using Thorlabs.MotionControl.GenericMotorCLI;
using Thorlabs.MotionControl.GenericMotorCLI.Settings;
using Thorlabs.MotionControl.KCube.DCServoCLI;
using Thorlabs.MotionControl.GenericMotorCLI.AdvancedMotor;
using FAST_Scan.Core;
using System.Windows;
using Thorlabs.MotionControl.GenericMotor;

namespace FAST_Scan.Core
{

    public static class DataStore
    {
        public static int[] Waveform = new int[1024];
    }

    internal class Scan
    {
        string serialNo_ServoX = "27261487";
        string serialNo_ServoY = "27261089";
        string serialNo_ServoZ = "27269670";


        KCubeDCServo ServoY = null;
        KCubeDCServo ServoX = null;
        KCubeDCServo ServoZ = null;

        //Constants
        const decimal stepInferiorLimit = 0.001m;
        const decimal positionLimit = 45;

        //Scan variables
        decimal initialPositionX = 0;
        decimal initialPositionY = 0;
        decimal initialPositionZ = 0;
        decimal finalPositionX = 0;
        decimal finalPositionY = 0;
        decimal finalPositionZ = 0;
        decimal stepX = 0;
        decimal stepY = 0;
        decimal stepZ = 0;
        int numStepsX = 0;
        int numStepsY = 0;
        int numStepsZ = 0;

        bool positionIsSetX = false;
        bool positionIsSetY = false;
        bool positionIsSetZ = false;

        PulsePolarity polarity;
        ScanType scanType;

        string filePath;

        int digitizerSamples;

        int intervalBinStart = 0;
        int intervalBinEnd = 1023;

        bool isStopped = true;

        Axis axis_1D;

        public enum ErrorStatus
        {
            NULL,
            OK,
            VALUE_OUT_OF_RANGE,
            INVALID_INPUT,
            CONFIGURE_DIGITIZER_FAIL,
            CONFIGURE_SERVO_FAIL,
            UNABLE_TO_HOME
        };
        
        enum PulsePolarity
        {
            NEGATVE,
            POSITIVE
        };

        public enum ScanType
        {
            SCAN_2D,
            SCAN_1D,
            FOCAL_ANALYSIS
        };

        public enum Axis
        {
            X   = 1,
            Y   = 2,
            XY  = 3,
            Z   = 4,
            XZ  = 5,
            YZ  = 6,
            XYZ = 7,
        };

        public static bool isTestMode()
        {
#if !TEST_MODE
            return false;
#else
            return true;
#endif
        }

        StatusMessage statusMessage;
        //Construtor
        public Scan(StatusMessage statusMessage, ScanType type, out ErrorStatus error)
        {
            this.statusMessage = statusMessage;
            scanType = type;
            error = ErrorStatus.NULL;

#if !TEST_MODE
            // Configure digitizer
            if (Convert.ToBoolean(Digitizer.Configure()))
            {
                error = ErrorStatus.CONFIGURE_DIGITIZER_FAIL;
                statusMessage.CreateStatusMessage("Unable to configure Digitizer");
                Logger.Instance.Log("Unable to configure Digitizer", LogType.Error);
                return;
            }
            else
            {
                statusMessage.CreateStatusMessage("Digitizer configured");
                Logger.Instance.Log("Digitizer configured", LogType.Info);
                error = ErrorStatus.OK;

            }

            ServosInit(out error);
#else
            statusMessage.CreateStatusMessage("Digitizer configured");
            Logger.Instance.Log("TEST_MODE: no real connection to devices!", LogType.Warning);
            Logger.Instance.Log("Digitizer configured: TEST_MODE", LogType.Info);
            error = ErrorStatus.OK;
#endif
        }

        public ErrorStatus Home(Axis axis)
        {   
            bool homeX = (axis == Axis.X) || (axis == Axis.XY) || (axis == Axis.XZ) || (axis == Axis.XYZ);
            bool homeY = (axis == Axis.Y) || (axis == Axis.XY) || (axis == Axis.YZ) || (axis == Axis.XYZ);
            bool homeZ = (axis == Axis.Z) || (axis == Axis.XZ) || (axis == Axis.YZ) || (axis == Axis.XYZ);
            bool error = false;

            if (HommingStateManager.ServoXHomed == false && homeX == true)
            {
                try
                {
                    statusMessage.CreateStatusMessage("Actuator X is Homing...");
                    Logger.Instance.Log("Actuator X is Homing...", LogType.Info);
#if !TEST_MODE
                    ServoX.Home(60000);
#endif
                    HommingStateManager.SetIsHomed(HommingStateManager.Servo.X, true);
                }
                catch
                {
                    statusMessage.CreateStatusMessage("Unable to Home ServoX");
                    Logger.Instance.Log("Unable to Home Servo X", LogType.Error);
                    error = true;
                }
            }
            if (HommingStateManager.ServoYHomed == false && homeY == true)
            {
                try
                {
                    statusMessage.CreateStatusMessage("Actuator Y is Homing...");
                    Logger.Instance.Log("Actuator Y is Homing...", LogType.Info);
#if !TEST_MODE
                    ServoY.Home(60000);
#endif
                    HommingStateManager.SetIsHomed(HommingStateManager.Servo.Y, true);
                }
                catch
                {
                    statusMessage.CreateStatusMessage("Unable to Home ServoY");
                    Logger.Instance.Log("Unable to Home Servo Y", LogType.Error);
                    error = true;
                }
            }
            if (HommingStateManager.ServoZHomed == false && homeZ == true)
            {
                try
                {
                    statusMessage.CreateStatusMessage("Actuator Z is Homing...");
                    Logger.Instance.Log("Actuator Z is Homing...", LogType.Info);
#if !TEST_MODE
                    ServoZ.Home(60000);
#endif
                    HommingStateManager.SetIsHomed(HommingStateManager.Servo.Z, true);
                }
                catch
                {
                    statusMessage.CreateStatusMessage("Unable to Home ServoZ");
                    Logger.Instance.Log("Unable to Home Servo Z", LogType.Error);
                    error = true;
                }
            }
            if (error == false)
            {
                return ErrorStatus.OK;
            }
            else
            {
                return ErrorStatus.UNABLE_TO_HOME;
            }
        }

        public ErrorStatus setInitialX(string input)  //pede posição inicial de X
        {
            if (decimal.TryParse(input, out initialPositionX))
            {
                if (initialPositionX < positionLimit) //limite de segurança para posição
                {
                    positionIsSetX = true;
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }

            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public ErrorStatus setInitialY(string input)  //pede posição inicial de Y
        {
            if (decimal.TryParse(input, out initialPositionY))
            {
                if (initialPositionY < positionLimit) //limite de segurança para posição
                {
                    positionIsSetY = true;
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }

            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public ErrorStatus setInitialZ(string input)  //pede posição inicial de Z
        {
            if (decimal.TryParse(input, out initialPositionZ))
            {
                if (initialPositionZ < positionLimit) //limite de segurança para posição
                {
                    positionIsSetZ = true;
                    statusMessage.CreateStatusMessage("Position Z set to: " + initialPositionZ.ToString() + " IsSet: " + positionIsSetZ.ToString());
                    Logger.Instance.Log("Position Z set to: " + initialPositionZ.ToString() + " IsSet: " + positionIsSetZ.ToString(), LogType.Info);
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }

            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public ErrorStatus setFinalX(string input)    //pede posição final de X
        {
            if (decimal.TryParse(input, out finalPositionX))
            {
                if (finalPositionX < positionLimit && finalPositionX > initialPositionX)
                {
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }
            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public ErrorStatus setFinalY(string input)    //pede posição final de Y
        {
            if (decimal.TryParse(input, out finalPositionY))
            {
                if (finalPositionY < positionLimit && finalPositionY > initialPositionY)
                {
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }
            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public ErrorStatus setFinalZ(string input)    //pede posição final de Z
        {
            if (decimal.TryParse(input, out finalPositionZ))
            {
                if (finalPositionZ < positionLimit && finalPositionZ > initialPositionZ)
                {
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }
            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public ErrorStatus setPaceX(string numberOfSteps)     //determina o passo em X
        {
            if (int.TryParse(numberOfSteps, out numStepsX))
            {
                //calcula step
                if (numStepsX == 0)
                {
                    stepX = 0;
                }
                else
                {
                    stepX = (finalPositionX - initialPositionX) / numStepsX;
                }

                //verifica erros
                if (stepX > stepInferiorLimit)
                {
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }
            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public ErrorStatus setPaceY(string numberOfSteps)     //determina o passo em Y
        {
            if (int.TryParse(numberOfSteps, out numStepsY))
            {   //calcula o step
                if(numStepsY == 0)
                {
                    stepY = 0;
                }
                else
                {
                    stepY = (finalPositionY - initialPositionY) / numStepsY;
                }
                //verifica erros
                if (stepY > stepInferiorLimit)
                {
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }
            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public ErrorStatus setPaceZ(string numberOfSteps)     //determina o passo em Z
        {
            if (int.TryParse(numberOfSteps, out numStepsZ))
            {   //calcula o step
                if (numStepsZ == 0)
                {
                    stepZ = 0;
                }
                else
                {
                    stepZ = (finalPositionZ - initialPositionZ) / numStepsZ;
                }
                //verifica erros
                if (stepZ > stepInferiorLimit)
                {
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }
            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        private void setAcquisitionIntervalInit()
        {
            
        }

        private void setAcquisitionIntervalFinal()
        {
            
        }
        
        public ErrorStatus setDigitizerSamplesValue(string value)
        {
            if (int.TryParse(value, out digitizerSamples))
            {
                if (digitizerSamples > 0) 
                {
                    return ErrorStatus.OK;
                }
                else
                {
                    return ErrorStatus.VALUE_OUT_OF_RANGE;
                }
            }
            else
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public ErrorStatus setPulsePolarity(string input)
        {
            if (String.Equals(input, "positive", StringComparison.OrdinalIgnoreCase))
            {
                polarity = PulsePolarity.POSITIVE;
                return ErrorStatus.OK;
            }
            else if (String.Equals(input, "negative", StringComparison.OrdinalIgnoreCase))
            {
                polarity = PulsePolarity.NEGATVE;
                return ErrorStatus.OK;
            }
            else 
            {
                return ErrorStatus.INVALID_INPUT;
            }
        }

        public void setOutputFilePath(string path)   //da o nome para o arquivo de output
        {
            filePath = path;
        }

        private void ServosInit(out ErrorStatus error)
        {
#if !TEST_MODE
            DeviceManagerCLI.BuildDeviceList();
#endif
            //configure ServoX
            try
            {
#if !TEST_MODE
                ServoX = KCubeDCServo.CreateKCubeDCServo(serialNo_ServoX);
#endif
                statusMessage.CreateStatusMessage("ServoX Configured.");
                Logger.Instance.Log("ServoX Configured.", LogType.Info);
#if !TEST_MODE
                ServoX.Connect(serialNo_ServoX);
#endif
                statusMessage.CreateStatusMessage("ServoX Connected.");
                Logger.Instance.Log("ServoX Connected.", LogType.Info);
                // Wait for the device settings to initialize. We ask the device to
                // throw an exception if this takes more than 5000ms (5s) to complete.
#if !TEST_MODE
                ServoX.WaitForSettingsInitialized(5000);
                // This calls LoadMotorConfiguration on the device to initialize the DeviceUnitConverter object required for real world unit parameters.
                MotorConfiguration motorSettings_ServoX = ServoX.LoadMotorConfiguration(serialNo_ServoX, DeviceConfiguration.DeviceSettingsUseOptionType.UseFileSettings);
                // This starts polling the device at intervals of 250ms (0.25s).
                ServoX.StartPolling(250);
                // We are now able to Enable the device otherwise any move is ignored. You should see a physical response from your controller.
                ServoX.EnableDevice();
#endif
                statusMessage.CreateStatusMessage("Servo X Enabled");
                Logger.Instance.Log("Servo X Enabled", LogType.Info);
                // Needs a delay to give time for the device to be enabled.
                Thread.Sleep(500);
            }
            catch
            {
                error = ErrorStatus.CONFIGURE_SERVO_FAIL;
                statusMessage.CreateStatusMessage("Unable to configure ServoX");
                Logger.Instance.Log("Unable to configure ServoX", LogType.Error);
                if (positionIsSetX)     //não permite continuar caso X seja necessario
                {
                    Digitizer.Close();
                    return;
                }
            }
            //configure ServoY
            try
            {
#if !TEST_MODE
                ServoY = KCubeDCServo.CreateKCubeDCServo(serialNo_ServoY);
#endif
                statusMessage.CreateStatusMessage("ServoY Configured.");
                Logger.Instance.Log("ServoY Configured.", LogType.Info);
#if !TEST_MODE
                ServoY.Connect(serialNo_ServoY);
#endif
                statusMessage.CreateStatusMessage("ServoY Connected.");
                Logger.Instance.Log("ServoY Connected.", LogType.Info);
#if !TEST_MODE
                ServoY.WaitForSettingsInitialized(5000);
                MotorConfiguration motorSettings_ServoY = ServoY.LoadMotorConfiguration(serialNo_ServoY, DeviceConfiguration.DeviceSettingsUseOptionType.UseFileSettings);
                ServoY.StartPolling(250);
                ServoY.EnableDevice();
#endif
                statusMessage.CreateStatusMessage("Servo Y Enabled");
                Logger.Instance.Log("Servo Y Enabled", LogType.Info);
                Thread.Sleep(500);
            }
            catch
            {
                error = ErrorStatus.CONFIGURE_SERVO_FAIL;
                statusMessage.CreateStatusMessage("Unable to configure ServoY");
                Logger.Instance.Log("Unable to configure ServoY", LogType.Error);
                if (positionIsSetY)     //não permite continuar caso Y seja necessario
                {
                    Digitizer.Close();
                    return;
                }
            }
            //configure ServoZ
            try
            {
#if !TEST_MODE
                ServoZ = KCubeDCServo.CreateKCubeDCServo(serialNo_ServoZ);
#endif
                statusMessage.CreateStatusMessage("ServoZ Configured.");
                Logger.Instance.Log("ServoZ Configured.", LogType.Info);
#if !TEST_MODE
                ServoZ.Connect(serialNo_ServoZ);
#endif
                statusMessage.CreateStatusMessage("ServoZ Connected.");
                Logger.Instance.Log("ServoZ Connected.", LogType.Info);
#if !TEST_MODE
                ServoZ.WaitForSettingsInitialized(5000);
                MotorConfiguration motorSettings_ServoZ = ServoZ.LoadMotorConfiguration(serialNo_ServoZ, DeviceConfiguration.DeviceSettingsUseOptionType.UseFileSettings);
                ServoZ.StartPolling(250);
                ServoZ.EnableDevice();
#endif
                statusMessage.CreateStatusMessage("Servo Z Enabled");
                Logger.Instance.Log("Servo Z Enabled", LogType.Info);
                Thread.Sleep(500);
            }
            catch
            {
                error = ErrorStatus.CONFIGURE_SERVO_FAIL;
                statusMessage.CreateStatusMessage("Unable to configure ServoZ");
                Logger.Instance.Log("Unable to configure ServoZ", LogType.Error);
                if (positionIsSetZ)     //não permite continuar caso Z seja necessario
                {
                    Digitizer.Close();
                    return;
                }
            }
            error = ErrorStatus.OK;
        }

        public void Execute()
        {
            isStopped = false;
            statusMessage.CreateStatusMessage("Scan initiated");
            Logger.Instance.Log("Scan initiated", LogType.Info);

            switch (scanType)
            {
                case ScanType.SCAN_2D:
                    {
#if !TEST_MODE
                        Scan_2D_exe();
#else
                        Scan_2D_Simulator();
#endif
                        break;
                    }
                case ScanType.SCAN_1D:
                    {
#if !TEST_MODE
                        Scan_1D_exe();
#else
#endif
                        break;
                    }
                case ScanType.FOCAL_ANALYSIS:
                    {
#if !TEST_MODE
                        Scan_Focal_exe();
#else
#endif
                        break;
                    }
            }
            isStopped = true;
            statusMessage.CreateStatusMessage("Scan Finished");
            Logger.Instance.Log("Scan Finished", LogType.Info);
        }

        private void Scan_2D_exe()
        {
            decimal PositionX;
            decimal PositionY;
            int amplitude=0;

            //Move os servos para a posição inicial
            statusMessage.CreateStatusMessage("Moving to initial position...");
            Logger.Instance.Log("Moving to initial position...", LogType.Info);
            ServoX.MoveTo(initialPositionX, 60000);
            ServoY.MoveTo(initialPositionY, 60000);

            //Caso a posição de Z seja configurada, move o servo Z
            if(positionIsSetZ == true)
            {
                ServoZ.MoveTo(initialPositionZ, 60000);
            }

            //Move relativo a posição inicial
            statusMessage.CreateStatusMessage("Scan in execution...");
            Logger.Instance.Log("Scan in execution...", LogType.Info);

            StreamWriter sw = null;
                
            using (sw = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                //escreve passo no outputFile
                sw.WriteLine("Step X = " + stepX);
                sw.WriteLine("Step Y = " + stepY);

                for (int i = 0; i <= numStepsY; i++)
                {
                    PositionY = i * stepY;

                    for (int j = 0; j <= numStepsX; j++)
                    {
                        if (ScanStateManager.StopScan == false)
                        {
                            //Se o pulso é negativo pega valor minimo, se positivo pega o valor máximo
                            if (polarity == PulsePolarity.NEGATVE)
                                amplitude = Digitizer.GetAvgMinValueInterval(digitizerSamples, intervalBinStart, intervalBinEnd);
                            else if (polarity == PulsePolarity.POSITIVE)
                                amplitude = Digitizer.GetAvgMaxValueInterval(digitizerSamples, intervalBinStart, intervalBinEnd);

                            PositionX = j * stepX;

                            statusMessage.CreateStatusMessage("Amplitude: " + amplitude.ToString() + "\tPosicao X: " + PositionX.ToString() + "\tPosicao Y: " + PositionY.ToString());
                            Logger.Instance.Log("Amplitude: " + amplitude.ToString() + "\tPosicao X: " + PositionX.ToString() + "\tPosicao Y: " + PositionY.ToString(), LogType.Data);

                            //escreve amplitude no outputFile
                            sw.Write(amplitude.ToString());

                            if (j != numStepsX)
                            {
                                sw.Write('\t');
                                ServoX.MoveRelative(MotorDirection.Forward, stepX, 60000);
                            }
                        }
                        else
                        {
                            isStopped = true;
                            ScanStateManager.SetStopScan(false);
                            return;
                        }
                    }
                    if (ScanStateManager.StopScan == false)
                    {
                        sw.Write('\n');
                        ServoX.MoveTo(initialPositionX, 60000);
                        if (i != numStepsY)
                        {
                            ServoY.MoveRelative(MotorDirection.Forward, stepY, 60000);
                        }
                    }
                    else
                    {
                        isStopped = true;
                        ScanStateManager.SetStopScan(false);
                        return;
                    }
                }
            }
        }

        private void Scan_1D_exe()
        {
            decimal Position;
            int amplitude = 0;

            int numSteps = 0;
            decimal step=0;

            switch (axis_1D)
            {
                case Axis.X:
                    {
                        numSteps = numStepsX; step = stepX; break;
                    }
                case Axis.Y:
                    {
                        numSteps = numStepsY; step = stepY; break;
                    }
                case Axis.Z:
                    {
                        numSteps = numStepsZ; step = stepZ; break;
                    }
                default:
                    {
                        statusMessage.CreateStatusMessage("ERROR! Unable to identify scan axis!");
                        Logger.Instance.Log("Unable to identify scan axis!", LogType.Error);
                        return;
                    }
            }

            //Move os servos para a posição inicial
            statusMessage.CreateStatusMessage("Moving to initial position...");
            Logger.Instance.Log("Moving to initial position...", LogType.Info);


            if (positionIsSetX)
                ServoX.MoveTo(initialPositionX, 60000);

            if (positionIsSetY)
                ServoY.MoveTo(initialPositionY, 60000);

            if (positionIsSetZ)
                ServoZ.MoveTo(initialPositionZ, 60000);



            statusMessage.CreateStatusMessage("Scan in execution...");
            Logger.Instance.Log("Scan in execution...", LogType.Info);

            StreamWriter sw = null;

            using (sw = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                //escreve cabecalho no outputFile
                sw.WriteLine("Scan Type: " + scanType.ToString() + "\tScan Axis: " + axis_1D.ToString());
                sw.WriteLine("Initial Position:\tX = " + initialPositionX.ToString() + "\tY = " + initialPositionY.ToString() + "\tZ = " + initialPositionZ.ToString());
                sw.WriteLine("Final Position:\t\tX = " + finalPositionX.ToString() + "\tY = " + finalPositionY.ToString() + "\tZ = " + finalPositionZ.ToString());
                sw.WriteLine("Step = " + step.ToString());

                for (int j = 0; j <= numSteps; j++)
                {
                    if (ScanStateManager.StopScan == false)
                    {
                        //Se o pulso é negativo pega valor minimo, se positivo pega o valor máximo
                        if (polarity == PulsePolarity.NEGATVE)
                            amplitude = Digitizer.GetAvgMinValueInterval(digitizerSamples, intervalBinStart, intervalBinEnd);
                        else if (polarity == PulsePolarity.POSITIVE)
                            amplitude = Digitizer.GetAvgMaxValueInterval(digitizerSamples, intervalBinStart, intervalBinEnd);

                        Position = j * step;

                        statusMessage.CreateStatusMessage("Amplitude: " + amplitude.ToString() + "\tPosition: " + Position.ToString());
                        Logger.Instance.Log("Amplitude: " + amplitude.ToString() + "\tPosition: " + Position.ToString(), LogType.Data);


                        //escreve amplitude no outputFile
                        sw.Write(amplitude.ToString());

                        if (j != numStepsX)
                        {
                            sw.Write('\t');
                            ServoX.MoveRelative(MotorDirection.Forward, stepX, 60000);
                        }
                    }
                    else
                    {
                        isStopped = true;
                        ScanStateManager.SetStopScan(false);
                        return;
                    }
                }               
            }
        }

        public void Set_1D_Axis(Axis axis)
        {
            axis_1D = axis;
        }

        public ErrorStatus set_1D_Initial_Position(string input)
        {
            switch(axis_1D)
            {
                case Axis.X:
                    return setInitialX(input);
                case Axis.Y:
                    return setInitialY(input);
                case Axis.Z:
                    return setInitialZ(input);
                default:
                    return ErrorStatus.NULL;
            }
        }

        public ErrorStatus set_1D_Final_Position(string input)
        {
            switch (axis_1D)
            {
                case Axis.X:
                    return setFinalX(input);
                case Axis.Y:
                    return setFinalY(input);
                case Axis.Z:
                    return setFinalZ(input);
                default:
                    return ErrorStatus.NULL;
            }
        }

        public ErrorStatus set_1D_Pace(string input)
        {
            switch (axis_1D)
            {
                case Axis.X:
                    return setPaceX(input);
                case Axis.Y:
                    return setPaceY(input);
                case Axis.Z:
                    return setPaceZ(input);
                default:
                    return ErrorStatus.NULL;
            }
        }

        private void Scan_Focal_exe()
        {

        }

        public void Close()
        {
            while (!isStopped)
            {
                statusMessage.CreateStatusMessage("Waiting for Scan to stop...");
                Logger.Instance.Log("Waiting for Scan to stop...", LogType.Warning);
                Thread.Sleep(500);
            }

            statusMessage.CreateStatusMessage("Scan Stopped. Closing Devices");
            Logger.Instance.Log("Scan Stopped. Closing Devices", LogType.Info);

#if !TEST_MODE
            Digitizer.Close();

            // Stop polling the device.
            ServoY.StopPolling();
            ServoX.StopPolling();
            ServoZ.StopPolling();
            // This shuts down the controller. This will use the Disconnect() function to close communications &will then close the used library.
            ServoY.ShutDown();
            ServoX.ShutDown();
            ServoZ.ShutDown();
#endif

            statusMessage.CreateStatusMessage("DevicesClosed");
            Logger.Instance.Log("DevicesClosed", LogType.Info);

            positionIsSetX = false;
            positionIsSetY = false;
            positionIsSetZ = false;

        }


        private void Scan_2D_Simulator()
        {
            decimal PositionX;
            decimal PositionY;
            int amplitude = 0;

            //Move os servos para a posição inicial
            statusMessage.CreateStatusMessage("Moving to initial position...");
            Logger.Instance.Log("Moving to initial position...", LogType.Info);

            //Move relativo a posição inicial
            statusMessage.CreateStatusMessage("Scan in execution...");
            Logger.Instance.Log("Scan in execution...", LogType.Info);

            //Loop de aquisição
            for (int i = 0; i <= numStepsY; i++)
            {
                PositionY = i * stepY;

                for (int j = 0; j <= numStepsX; j++)
                {
                    if (ScanStateManager.StopScan == false)
                    {
                        //Se o pulso é negativo pega valor minimo, se positivo pega o valor máximo
                        if (polarity == PulsePolarity.NEGATVE)
                            amplitude = Digitizer.GetMinAvgFromWaveform_Sim(DataStore.Waveform, digitizerSamples, intervalBinStart, intervalBinEnd);
                        else if (polarity == PulsePolarity.POSITIVE)
                            amplitude = Digitizer.GetMaxAvgFromWaveform_Sim(DataStore.Waveform, digitizerSamples, intervalBinStart, intervalBinEnd);

                        PositionX = j * stepX;

                        statusMessage.CreateStatusMessage("Amplitude: " + amplitude.ToString() + "\tPosicao X: " + PositionX.ToString() + "\tPosicao Y: " + PositionY.ToString());
                        Logger.Instance.Log("Amplitude: " + amplitude.ToString() + "\tPosicao X: " + PositionX.ToString() + "\tPosicao Y: " + PositionY.ToString(), LogType.Data);

                        //escreve amplitude no outputFile
                        if (j != numStepsX)
                        {   //simula passo do motor
                            Task.Delay(250).Wait();
                            //ServoX.MoveRelative(MotorDirection.Forward, stepX, 60000);
                        }
                    }
                    else
                    {
                        isStopped = true;
                        ScanStateManager.SetStopScan(false);
                        return;
                    }
                }
                if (ScanStateManager.StopScan == false)
                {   //simula passo do motor
                    Task.Delay(250).Wait();
                    //ServoX.MoveTo(initialPositionX, 60000);
                    if (i != numStepsY)
                    {   //simula passo do motor
                        Task.Delay(250).Wait();
                        //ServoY.MoveRelative(MotorDirection.Forward, stepY, 60000);
                    }
                }
                else
                {
                    isStopped = true;
                    ScanStateManager.SetStopScan(false);
                    return;
                }
            }
        }

    }

}