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
    internal class Scan
    {
        string serialNo_ServoY = "27261089";
        string serialNo_ServoX = "27261487";
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
        int intervalBinEnd = 1024;

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

        StatusMessage statusMessage;
        //Construtor
        public Scan(StatusMessage statusMessage, ScanType type, out ErrorStatus error)
        {
            this.statusMessage = statusMessage;
            scanType = type;
            error = ErrorStatus.NULL;

            // Configure digitizer
            if (Convert.ToBoolean(Digitizer.Configure()))
            {
                error = ErrorStatus.CONFIGURE_DIGITIZER_FAIL;
                statusMessage.CreateStatusMessage("Unable to configure Digitizer");
                return;
            }
            else
            {
                statusMessage.CreateStatusMessage("Digitizer configured");
            }

            ServosInit(out error);
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
                    ServoX.Home(60000);
                    HommingStateManager.SetIsHomed(HommingStateManager.Servo.X, true);
                }
                catch
                {
                    statusMessage.CreateStatusMessage("Unable to Home ServoX");
                    error = true;
                }
            }
            if (HommingStateManager.ServoYHomed == false && homeY == true)
            {
                try
                {
                    statusMessage.CreateStatusMessage("Actuator Y is Homing...");
                    ServoY.Home(60000);
                    HommingStateManager.SetIsHomed(HommingStateManager.Servo.Y, true);
                }
                catch
                {
                    statusMessage.CreateStatusMessage("Unable to Home ServoY");
                    error = true;
                }
            }
            if (HommingStateManager.ServoZHomed == false && homeZ == true)
            {
                try
                {
                    statusMessage.CreateStatusMessage("Actuator Z is Homing...");
                    ServoZ.Home(60000);
                    HommingStateManager.SetIsHomed(HommingStateManager.Servo.Z, true);
                }
                catch
                {
                    statusMessage.CreateStatusMessage("Unable to Home ServoZ");
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
            DeviceManagerCLI.BuildDeviceList();

            //configure ServoX
            try
            {
                ServoX = KCubeDCServo.CreateKCubeDCServo(serialNo_ServoX);
                statusMessage.CreateStatusMessage("ServoZ Configured.");
                ServoX.Connect(serialNo_ServoX);
                statusMessage.CreateStatusMessage("ServoZ Connected.");
                // Wait for the device settings to initialize. We ask the device to
                // throw an exception if this takes more than 5000ms (5s) to complete.
                ServoX.WaitForSettingsInitialized(5000);
                // This calls LoadMotorConfiguration on the device to initialize the DeviceUnitConverter object required for real world unit parameters.
                MotorConfiguration motorSettings_ServoX = ServoX.LoadMotorConfiguration(serialNo_ServoX, DeviceConfiguration.DeviceSettingsUseOptionType.UseFileSettings);
                // This starts polling the device at intervals of 250ms (0.25s).
                ServoX.StartPolling(250);
                // We are now able to Enable the device otherwise any move is ignored. You should see a physical response from your controller.
                ServoX.EnableDevice();
                statusMessage.CreateStatusMessage("Servo X Enabled");
                // Needs a delay to give time for the device to be enabled.
                Thread.Sleep(500);
            }
            catch
            {
                error = ErrorStatus.CONFIGURE_SERVO_FAIL;
                statusMessage.CreateStatusMessage("Unable to configure ServoX");
                if (true)     //não permite continuar caso X seja necessario
                {
                    Digitizer.Close();
                    return;
                }
            }
            //configure ServoY
            try
            {
                ServoY = KCubeDCServo.CreateKCubeDCServo(serialNo_ServoY);
                statusMessage.CreateStatusMessage("ServoY Configured.");
                ServoY.Connect(serialNo_ServoY);
                statusMessage.CreateStatusMessage("ServoY Connected.");
                ServoY.WaitForSettingsInitialized(5000);
                MotorConfiguration motorSettings_ServoY = ServoY.LoadMotorConfiguration(serialNo_ServoY, DeviceConfiguration.DeviceSettingsUseOptionType.UseFileSettings);
                ServoY.StartPolling(250);
                ServoY.EnableDevice();
                Thread.Sleep(500);
            }
            catch
            {
                error = ErrorStatus.CONFIGURE_SERVO_FAIL;
                statusMessage.CreateStatusMessage("Unable to configure ServoY");
                if (true)     //não permite continuar caso Y seja necessario
                {
                    Digitizer.Close();
                    return;
                }
            }
            //configure ServoZ
            try
            {
                ServoZ = KCubeDCServo.CreateKCubeDCServo(serialNo_ServoZ);
                statusMessage.CreateStatusMessage("ServoZ Configured.");
                ServoZ.Connect(serialNo_ServoZ);
                statusMessage.CreateStatusMessage("ServoZ Connected.");
                ServoZ.WaitForSettingsInitialized(5000);
                MotorConfiguration motorSettings_ServoZ = ServoZ.LoadMotorConfiguration(serialNo_ServoZ, DeviceConfiguration.DeviceSettingsUseOptionType.UseFileSettings);
                ServoZ.StartPolling(250);
                ServoZ.EnableDevice();
                Thread.Sleep(500);
            }
            catch
            {
                error = ErrorStatus.CONFIGURE_SERVO_FAIL;
                statusMessage.CreateStatusMessage("Unable to configure ServoZ");
                if (scanType == ScanType.FOCAL_ANALYSIS)     //não permite continuar caso Z seja necessario
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

            switch (scanType)
            {
                case ScanType.SCAN_2D:
                    {
                        Scan_2D_exe();
                        break;
                    }
                case ScanType.SCAN_1D:
                    {
                        Scan_1D_exe();
                        break;
                    }
                case ScanType.FOCAL_ANALYSIS:
                    {
                        Scan_Focal_exe();
                        break;
                    }
            }
            isStopped = true;
            statusMessage.CreateStatusMessage("Scan Finished");
        }

        private void Scan_2D_exe()
        {
            decimal PositionX;
            decimal PositionY;
            int amplitude=0;

            //Move os servos para a posição inicial
            statusMessage.CreateStatusMessage("Moving to initial position...");
            ServoX.MoveTo(initialPositionX, 60000);
            ServoY.MoveTo(initialPositionY, 60000);

            //Move relativo a posição inicial
            statusMessage.CreateStatusMessage("Scan in execution...");

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
                        if (ScanStateManager.ScanRunning == true)
                        {
                            //Se o pulso é negativo pega valor minimo, se positivo pega o valor máximo
                            if (polarity == PulsePolarity.NEGATVE)
                                amplitude = Digitizer.GetAvgMinValueInterval(digitizerSamples, intervalBinStart, intervalBinEnd);
                            else if (polarity == PulsePolarity.POSITIVE)
                                amplitude = Digitizer.GetAvgMaxValueInterval(digitizerSamples, intervalBinStart, intervalBinEnd);

                            PositionX = j * stepX;

                            statusMessage.CreateStatusMessage("Amplitude: " + amplitude.ToString() + "\tPosicao X: " + PositionX.ToString() + "\tPosicao Y: " + PositionY.ToString());

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
                            return;
                        }
                    }
                    if (ScanStateManager.ScanRunning == true)
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
            }

            //Move os servos para a posição inicial
            statusMessage.CreateStatusMessage("Moving to initial position...");

            if (positionIsSetX)
            {
                ServoX.MoveTo(initialPositionX, 60000);
            }
            if (positionIsSetY)
            {
                ServoY.MoveTo(initialPositionY, 60000);
            }
            if (positionIsSetZ)
            {
                ServoZ.MoveTo(initialPositionZ, 60000);
            }

            statusMessage.CreateStatusMessage("Scan in execution...");

            StreamWriter sw = null;

            using (sw = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                //escreve cabecalho no outputFile
                sw.WriteLine("Scan Type: " + scanType.ToString() + "\tScan Axis: " + axis_1D.ToString());
                sw.WriteLine("Initial Position:\tX = " + initialPositionX.ToString() + "\tY = " + initialPositionY.ToString() + "\tY = " + initialPositionZ.ToString());
                sw.WriteLine("Final Position:\tX = " + finalPositionX.ToString() + "\tY = " + finalPositionY.ToString() + "\tY = " + finalPositionZ.ToString());
                sw.WriteLine("Step = " + step.ToString());

                for (int j = 0; j <= numSteps; j++)
                {
                    if (ScanStateManager.ScanRunning == true)
                    {
                        //Se o pulso é negativo pega valor minimo, se positivo pega o valor máximo
                        if (polarity == PulsePolarity.NEGATVE)
                            amplitude = Digitizer.GetAvgMinValueInterval(digitizerSamples, intervalBinStart, intervalBinEnd);
                        else if (polarity == PulsePolarity.POSITIVE)
                            amplitude = Digitizer.GetAvgMaxValueInterval(digitizerSamples, intervalBinStart, intervalBinEnd);

                        Position = j * step;

                        statusMessage.CreateStatusMessage("Amplitude: " + amplitude.ToString() + "\tPosition: " + Position.ToString());

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
                Thread.Sleep(100);
            }

            statusMessage.CreateStatusMessage("Scan Stopped. Closing Devices");

            Digitizer.Close();

            // Stop polling the device.
            ServoY.StopPolling();
            ServoX.StopPolling();
            // This shuts down the controller. This will use the Disconnect() function to close communications &will then close the used library.
            ServoY.ShutDown();
            ServoX.ShutDown();

            statusMessage.CreateStatusMessage("DevicesClosed");

        }

    }

}