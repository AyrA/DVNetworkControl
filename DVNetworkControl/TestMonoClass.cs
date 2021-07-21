using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace DVNetworkControl
{
    public static class TestModClass
    {
        private static LocoControllerBase CurrentLoco = null;
        private static TrainCar CurrentCar = null;
        private static UdpClient listener = null;
        private static bool init = false;

        public static void Start()
        {
            if (!init)
            {
                listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 31337));
                listener.BeginReceive(DataIn, null);
                init = true;
                PlayerManager.CarChanged += PlayerManager_CarChanged;
                Debug.LogWarning($"{nameof(TestModClass)} {Assembly.GetExecutingAssembly().GetName().Version} initialized");
            }
        }

        public static void Stop()
        {
            if (init)
            {
                PlayerManager.CarChanged -= PlayerManager_CarChanged;
                init = false;
                CurrentLoco = null;
                listener.Dispose();
                Debug.LogWarning($"{nameof(TestModClass)} stopped");
            }
        }

        private static void DataIn(IAsyncResult ar)
        {
            if (init)
            {
                byte[] data = null;
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    data = listener.EndReceive(ar, ref remoteEP);
                }
                catch
                {
                    Debug.LogWarning($"{nameof(TestModClass)}: Socket error");
                }
                ProcessPacket(data, remoteEP);
                listener.BeginReceive(DataIn, null);
            }
        }

        private static void ProcessPacket(byte[] data, IPEndPoint remoteEP)
        {
            var Lines = Encoding.UTF8.GetString(data).Split('\0');
            foreach (var Line in Lines)
            {
                if (Line.Contains("="))
                {
                    var Command = Line.Split('=')[0].Trim();
                    var Value = Line.Substring(Line.IndexOf("=") + 1).Trim();
                    Debug.Log($"Command from {remoteEP}: {Command}={Value}");
                    //Lines that set values
                    switch (Command.ToUpper())
                    {
                        case "CAMERA":
                            SendCommandReply(RotateCamera(Value), remoteEP);
                            break;
                        case "THROTTLE":
                            SendCommandReply(LocoCommand(CurrentLoco, "SetThrottle", Value), remoteEP);
                            break;
                        case "INDEPENDENTBRAKE":
                            SendCommandReply(LocoCommand(CurrentLoco, "SetIndependentBrake", Value), remoteEP);
                            break;
                        case "TRAINBRAKE":
                            SendCommandReply(LocoCommand(CurrentLoco, "SetBrake", Value), remoteEP);
                            break;
                        case "REVERSER":
                            SendCommandReply(LocoCommand(CurrentLoco, "SetReverser", Value), remoteEP);
                            break;
                        case "ENGINE":
                            SendCommandReply(LocoCommand(CurrentLoco, "SetEngineRunning", Value), remoteEP);
                            break;
                        case "SANDER":
                            SendCommandReply(LocoCommand(CurrentLoco, "SetSander", Value), remoteEP);
                            break;
                        case "HORN":
                            SendCommandReply(LocoCommand(CurrentLoco, "UpdateHorn", Value), remoteEP);
                            break;
                        default:
                            SendPacket(new string[] { $"ERROR=Unknown Command: {Command}" }, remoteEP);
                            break;
                    }
                }
                else
                {
                    Debug.Log($"Command from {remoteEP}: {Line}");
                    //Simple queries
                    switch (Line.Trim().ToUpper())
                    {
                        case "INFO":
                            if (CurrentLoco != null)
                            {
                                if (CurrentLoco is LocoControllerHandcar)
                                {
                                    SendPacket(new string[] { $"ERROR=Handcar does not support network operation" }, remoteEP);
                                    break;
                                }
                                var bs = CurrentLoco.train.brakeSystem;
                                //Basic loco stats that apply to all locos
                                List<string> BasicStats = new List<string>(new string[] {
                                    $"LOCO={CurrentLoco.name}",
                                    $"TYPE={CurrentLoco.train.carType}",
                                    $"THROTTLE={CurrentLoco.throttle}",
                                    $"INDEPENDENTBRAKE={CurrentLoco.independentBrake}",
                                    $"TRAINBRAKE={CurrentLoco.brake}",
                                    $"REVERSER={CurrentLoco.reverser}",
                                    $"SANDER={(CurrentLoco.IsSandOn() ? 1 : 0)}",
                                    $"SLIP={(CurrentLoco.IsWheelslipping() ? 1 : 0)}",
                                    $"SPEED={CurrentLoco.GetSpeedKmH() * (CurrentLoco.GetForwardSpeed() >= 0f ? 1 : -1)}",
                                    $"MAINPRESSURE={bs.mainReservoirPressure}",
                                    $"PIPEPRESSURE={bs.brakePipePressure}"
                                });
                                if (bs.hasCompressor)
                                {
                                    BasicStats.Add($"COMPRESSOR={(bs.compressorRunning ? 1 : 0)}");
                                }
                                float sand = float.NaN;
                                if (CurrentLoco is LocoControllerShunter)
                                {
                                    var L = CurrentLoco as LocoControllerShunter;
                                    sand = L.GetSandAmount();
                                    BasicStats.AddRange(new string[] {
                                        $"ENGINE={(L.GetEngineRunning() ? 1 : 0)}",
                                        $"TEMP={L.GetEngineTemp()}",
                                        $"RPM={L.GetEngineRPM()}",
                                        $"OIL={L.GetOilAmount()}",
                                        $"FUEL={L.GetFuelAmount()}"
                                    });
                                }
                                if (CurrentLoco is LocoControllerDiesel)
                                {
                                    var L = CurrentLoco as LocoControllerDiesel;
                                    sand = L.GetSandAmount();
                                    BasicStats.AddRange(new string[] {
                                        $"ENGINE={(L.GetEngineRunning() ? 1 : 0)}",
                                        $"TEMP={L.GetEngineTemp()}",
                                        $"RPM={L.GetEngineRPMGauge()}",
                                        $"OIL={L.GetOilAmount()}",
                                        $"FUEL={L.GetFuelAmount()}"
                                    });
                                }
                                if (CurrentLoco is LocoControllerSteam)
                                {
                                    var L = CurrentLoco as LocoControllerSteam;
                                    sand = L.GetSandAmount();
                                    BasicStats.AddRange(new string[] {
                                        $"FIRE={(L.IsFireOn() ? 1 : 0)}",
                                        $"TEMP={L.GetFireTemperature()}/{L.GetMaxFireTemperature()}",
                                        $"PRESSURE={L.GetBoilerPressure()}/{L.GetMaxBoilerPressure()}",
                                        $"WATER={L.GetBoilerWater()}/{L.GetBoilerWaterCapacity()}",
                                        $"TENDER={L.GetTenderWater()}/{L.GetTenderWaterCapacity()}",
                                        $"COAL={L.GetCoalInFirebox()}",
                                        $"DRAFT={L.GetDraft()}",
                                        $"BLOWER={L.GetBlower()}"
                                    });
                                }
                                if (!float.IsNaN(sand))
                                {
                                    BasicStats.Add($"SAND={sand}");
                                }
                                SendPacket(BasicStats, remoteEP);
                            }
                            else
                            {
                                SendPacket(new string[] { "LOCO=" }, remoteEP);
                            }
                            break;
                        default:
                            SendPacket(new string[] { $"ERROR=Unknown Command: {Line}" }, remoteEP);
                            break;
                    }
                }
            }
        }

        private static bool RotateCamera(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            var coords = value.Split(',');

            if (coords.Length == 3)
            {
                try
                {
                    var floats = coords.Select(float.Parse).ToArray();
                    //var oldRotation = PlayerManager.PlayerCamera.transform.rotation;
                    //var rotation = PlayerManager.PlayerCamera.transform.localRotation * Quaternion.Euler(floats[0], floats[1], floats[2]);
                    var instance = SingletonBehaviour<APlayerTeleport>.Instance;
                    if (instance is PlayerTeleportNonVR)
                    {
                        var c = (instance as PlayerTeleportNonVR).charController;
                        var y = c.m_Camera.transform.rotation.eulerAngles.y;
                        var rotation = Quaternion.Euler(0, y + floats[1], 0);
                        Debug.LogWarning(string.Format("w={0} x={1} y={2} z={3}",
                            c.m_Camera.transform.rotation.w,
                            c.m_Camera.transform.rotation.x,
                            c.m_Camera.transform.rotation.y,
                            c.m_Camera.transform.rotation.z));
                        c.ForceLookRotation(rotation);
                    }
                    //PlayerManager.TeleportPlayer(PlayerManager.GetWorldAbsolutePlayerPosition(), rotation, PlayerManager.PlayerCamera.transform, true);
                    //PlayerManager.PlayerCamera.transform.Rotate(new Vector3(floats[0], floats[1], floats[2]));
                    //PlayerManager.SetPlayer(PlayerManager.PlayerCamera.transform, PlayerManager.PlayerCamera);
                    return true;
                }
                catch
                {
                    Debug.LogWarning("Invalid coordinates: " + value);
                }
            }
            else if (value.Length == 1)
            {
                switch (value.ToUpper()[0])
                {
                    case 'R':
                        PlayerManager.PlayerCamera.transform.Rotate(new Vector3(0f, 90f * 1, 0f));
                        return true;
                    case 'L':
                        PlayerManager.PlayerCamera.transform.Rotate(new Vector3(0f, 90f * 3, 0f));
                        return true;
                    case 'B':
                        PlayerManager.PlayerCamera.transform.Rotate(new Vector3(0f, 90f * 2, 0f));
                        return true;
                }
            }
            return false;
        }

        private static void SendCommandReply(bool success, IPEndPoint remoteEP)
        {
            SendPacket(new string[] { success ? "OK" : "ERROR=Failed to set value" }, remoteEP);
        }

        private static bool LocoCommand(LocoControllerBase loco, string prop, string value)
        {
            if (loco == null)
            {
                return false;
            }
            var m = loco.GetType().GetMethod(prop);
            if (m != null)
            {
                var mParams = m.GetParameters();
                if (mParams.Length == 0)
                {
                    m.Invoke(loco, null);
                    return true;
                }
                var t = mParams[0].ParameterType;
                if (t == typeof(string))
                {
                    m.Invoke(loco, new object[] { value });
                }
                else if (t == typeof(int))
                {
                    if (!int.TryParse(value, out int i))
                    {
                        return false;
                    }
                    m.Invoke(loco, new object[] { i });
                }
                else if (t == typeof(float))
                {
                    if (!float.TryParse(value, out float f))
                    {
                        return false;
                    }
                    m.Invoke(loco, new object[] { f });
                }
                else if (t == typeof(double))
                {
                    if (!double.TryParse(value, out double d))
                    {
                        return false;
                    }
                    m.Invoke(loco, new object[] { d });
                }
                else if (t == typeof(bool))
                {
                    if (!bool.TryParse(value, out bool b))
                    {
                        return false;
                    }
                    m.Invoke(loco, new object[] { b });
                }
                return true;
            }
            return false;
        }

        private static void SendPacket(IEnumerable<string> Lines, IPEndPoint remoteEP)
        {
            if (Lines != null)
            {
                byte[] Data = Encoding.UTF8.GetBytes(string.Join("\0", Lines));
                try
                {
                    listener.Send(Data, Data.Length, remoteEP);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{nameof(TestModClass)}: Socket send error: {ex.Message}");
                }
            }
        }

        private static void PlayerManager_CarChanged(TrainCar obj)
        {
            if (obj != null)
            {
                Debug.LogWarning($"Player car change. New car: {obj.GetType().FullName}");
                LocoControllerShunter loco;
                try
                {
                    loco = obj.GetComponent<LocoControllerShunter>();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Car lacks loco. Error: {ex.Message}");
                    return;
                }
                CurrentCar = obj;
                CurrentLoco = loco;
            }
            else
            {
                CurrentLoco = null;
                CurrentCar = null;
                Debug.LogWarning($"Player car change. New car: <none>");
            }
        }
    }
}
