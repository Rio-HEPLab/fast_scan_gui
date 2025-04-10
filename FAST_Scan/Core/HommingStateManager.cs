using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    internal class HommingStateManager
    {
        public enum Servo
        {
            X,
            Y,
            Z
        };

        public static bool ServoXHomed { get; private set; }
        public static bool ServoYHomed { get; private set; }
        public static bool ServoZHomed { get; private set; }

        public static event Action<Servo, bool> OnServoHomedChanged;

        public static void SetIsHomed(Servo servoAxis, bool isHomed)
        {
            switch (servoAxis)
            {
                case Servo.X:
                    ServoXHomed = isHomed;
                    break;
                case Servo.Y:
                    ServoYHomed = isHomed;
                    break;
                case Servo.Z:
                    ServoZHomed = isHomed;
                    break;
            }

            OnServoHomedChanged?.Invoke(servoAxis, isHomed);
        }
    }
}
