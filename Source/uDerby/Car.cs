using System;
using System.Threading;
using Microsoft.SPOT;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using uScoober.Extensions;
using uScoober.Hardware;
using uScoober.Hardware.Light;
using uScoober.Hardware.Motors;
using uScoober.Hardware.Spot;
using uScoober.Threading;

namespace uDerby
{
    using Board = uScoober.Hardware.Boards.NetduinoPlus2;

    public class Car
    {
        private readonly Task _lightsManager;
        private readonly IDigitalInput _modeSwitch;
        private readonly Board _netduino;
        private readonly BrushlessSpeedController _speedController;
        private readonly IDigitalInput _triggerLock;
        private CancellationSource _cancelRace;
        private Modes _mode;
        private int _wantedPower;

        public Car()
        {
            _netduino = new Board();
            _speedController = new BrushlessSpeedController(_netduino.PwmOut.D5);

            //initialize input listeners
            _modeSwitch = new SpotDigitalInput(Pins.GPIO_PIN_D1, "mode switch", ResistorMode.PullUp,
                InterruptMode.InterruptEdgeBoth, 50);
            _modeSwitch.InvertReading = true;
            _modeSwitch.OnInterupt += (source, state, time) =>
            {
                _mode = _modeSwitch.Read() ? Modes.Staging : Modes.LightShow;
                _speedController.Stop();
            };

            _triggerLock = new SpotDigitalInput(Pins.GPIO_PIN_D2, "trigger-lock/e-stop", ResistorMode.PullUp,
                InterruptMode.InterruptEdgeBoth, 100);
            _triggerLock.InvertReading = true;
            _triggerLock.OnInterupt += (source, state, time) =>
            {
                //fire on button down, coud use InterruptMode.InterruptEdgeLow
                if (!_triggerLock.Read())
                {
                    return;
                }

                switch (_mode)
                {
                    case Modes.LightShow:
                    case Modes.Celebrate:
                    case Modes.Arming:
                        return;

                    case Modes.Staging:
                        if (!StartingPinFound())
                        {
                            return;
                        }
                        _mode = Modes.Arming;
                        Thread.Sleep(1500);
                        if (!StartingPinFound())
                        {
                            _mode = Modes.Staging;
                            return;
                        }
                        SnapshotWantedPower();
                        _cancelRace = new CancellationSource();
                        Task.Run(token =>
                        {
                            while (StartingPinFound())
                            {
                                _mode = Modes.Armed;
                                //wait for the pin to drop, or cancellation
                                token.ThrowIfCancellationRequested();
                            }
                            //Go Baby Go: fire all engines!
                            _mode = Modes.Race;
                            _speedController.SetPower(_wantedPower);
                            // todo: how long should we run?
                            int counter = 0;
                            while (counter < 2500)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    _speedController.Stop();
                                    token.ThrowIfCancellationRequested();
                                }
                                counter++;
                                Thread.Sleep(1);
                            }
                        },
                            _cancelRace)
                            .ContinueWith(previous =>
                            {
                                _speedController.Stop();
                                _mode = (previous.IsCanceled) ? Modes.LightShow : Modes.Celebrate;
                                _cancelRace = null;
                            });
                        return;

                    case Modes.Armed:
                    case Modes.Race:
                        _speedController.Stop();
                        _cancelRace.Cancel();
                        return;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            };

            _lightsManager = Task.New(() =>
            {
                var leftFrontLed = new DigitalLed(new SpotDigitalOutput(Pins.GPIO_PIN_D13, false, "Left Front"));
                var rightFrontLed = new DigitalLed(new SpotDigitalOutput(Pins.GPIO_PIN_D12, false, "Right Front"));
                var leftRearLed = new DigitalLed(new SpotDigitalOutput(Pins.GPIO_PIN_D11, false, "Left Rear"));
                var rightRearLed = new DigitalLed(new SpotDigitalOutput(Pins.GPIO_PIN_D10, false, "Right Rear"));
                var random = new Random();
                int counter1 = 0;

                while (true)
                {
                    bool flag;
                    switch (_mode)
                    {
                        case Modes.LightShow:
                            counter1 = (counter1 + 1)%50;
                            if (counter1 == 1 || counter1 == 26)
                            {
                                leftFrontLed.IsOn = random.NextBool();
                                leftRearLed.IsOn = random.NextBool();
                                rightFrontLed.IsOn = random.NextBool();
                                rightRearLed.IsOn = random.NextBool();
                            }
                            _netduino.OnboardLed.TurnOn(counter1/50.0);
                            break;
                        case Modes.Staging:
                            // blink left and right, front and back together
                            counter1 = (counter1 + 1)%50;
                            flag = counter1 < 25;
                            leftFrontLed.IsOn = flag;
                            leftRearLed.IsOn = flag;
                            rightFrontLed.IsOn = !flag;
                            rightRearLed.IsOn = !flag;
                            _netduino.OnboardLed.IsOn = counter1 < 5;
                            break;
                        case Modes.Arming:
                            counter1 = (counter1 + 1)%10;
                            flag = counter1 < 5;
                            leftFrontLed.IsOn = flag;
                            leftRearLed.IsOn = flag;
                            rightFrontLed.IsOn = flag;
                            rightRearLed.IsOn = flag;
                            _netduino.OnboardLed.IsOn = flag;
                            break;
                        case Modes.Armed:
                            // blink left and right front, rears on
                            counter1 = (counter1 + 1)%50;
                            flag = counter1 < 25;
                            leftFrontLed.IsOn = flag;
                            leftRearLed.IsOn = true;
                            rightFrontLed.IsOn = !flag;
                            rightRearLed.IsOn = true;
                            _netduino.OnboardLed.TurnOn();
                            break;
                        case Modes.Race:
                            // fronts on, rears off
                            leftFrontLed.IsOn = true;
                            leftRearLed.IsOn = false;
                            rightFrontLed.IsOn = true;
                            rightRearLed.IsOn = false;
                            _netduino.OnboardLed.TurnOff();
                            break;
                        case Modes.Celebrate:
                            // fronts fast random, rears blink together
                            counter1 = (counter1 + 1)%50;
                            flag = counter1 < 25;
                            if (counter1 == 1 || counter1 == 26)
                            {
                                leftFrontLed.IsOn = random.NextBool();
                                rightFrontLed.IsOn = random.NextBool();
                            }
                            leftRearLed.IsOn = flag;
                            rightRearLed.IsOn = flag;
                            _netduino.OnboardLed.TurnOn((50 - counter1)/50.0);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    Thread.Sleep(10);
                }
            });
        }

        public void Start()
        {
            _netduino.OnboardLed.TurnOn();
            _speedController.Arm();
            _mode = Modes.LightShow;
            _modeSwitch.InteruptEnabled = true;
            _triggerLock.InteruptEnabled = true;
            _lightsManager.Start();
        }

        private void SnapshotWantedPower()
        {
            // 2 part scale
            // 0v to 1.6v => 0% to 50%
            // 1.6v to 2.15v => 50% to 100%

            double reading = _netduino.AnalogIn[2].Read();
            if (reading < 1.6)
            {
                _wantedPower = (int) ((reading*50)/1.6);
            }
            else
            {
                _wantedPower = 50 + (int) (((reading - 1.6)*50)/0.55);
            }

            Debug.Print("Wanted Power:" + _wantedPower);
        }

        private bool StartingPinFound()
        {
            double light = _netduino.AnalogIn.A0.Read();
            Debug.Print("Start Light:" + light);
            return light >= 1.6;
        }

        private enum Modes
        {
            LightShow,
            Staging,
            Arming,
            Armed,
            Race,
            Celebrate
        }
    }
}