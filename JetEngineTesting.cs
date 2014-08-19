using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace JetEngineTesting
{

    public class Csv
    {
        int column = 0;
        Dictionary<string, int> columns = new Dictionary<string, int>();

        List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();

        Dictionary<string, object> current = null;

        public void Row()
        {
            if (current != null) rows.Add(current);
            current = null;
        }

        public void Add(string key, object value)
        {
            if (current != null && current.ContainsKey(key)) Row();
            if (current == null) current = new Dictionary<string, object>();
            if (!columns.ContainsKey(key)) columns[key] = column++;
            current[key] = value;
        }

        static System.Text.RegularExpressions.Regex reQuotes = new System.Text.RegularExpressions.Regex("\"");
        static System.Text.RegularExpressions.Regex reNewLine = new System.Text.RegularExpressions.Regex("(\r\n|\r|\n)",System.Text.RegularExpressions.RegexOptions.Singleline);
        string Escape(string val)
        {
            return "\""+reQuotes.Replace(reNewLine.Replace(val, "\\n"), "\"\"")+"\"";
        }

        public void Save(string filename)
        {
            var cols = columns.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
            using (var o = new System.IO.StreamWriter(filename))
            {
                // write header
                {
                    var header = "";
                    var mid = "";
                    foreach (var col in cols)
                    {
                        header += mid+Escape(col);
                        mid = ";";
                    }
                    o.WriteLine(header);
                }
                // then rows
                foreach (var row in rows)
                {
                    var line = "";
                    var mid = "";
                    foreach (var col in cols)
                    {
                        var val = "";
                        if (row.ContainsKey(col)) val = row[col].ToString();
                        line += mid + Escape(val);
                        mid = ";";
                    }
                    o.WriteLine(line);
                }
                o.Flush();
                o.Close();
            }
        }
    }


    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class JetEngineTesting : MonoBehaviour
    {

        void Awake()
        {
            Debug.Log("[JetEngineTesting] Awake");
        }

        double time;
        float fixedDeltaTime;

        Vessel vessel = null;
        string vesselName = null;
        string engineName = null;

        float finalThrust;
        float fuelFlowGui;
        string status;
        string statusL2;
        float thrustPercentage;

        float currentThrottle;
        bool EngineIgnited;
        float maxThrust;
        float minThrust;
        float requestedThrottle;
        float requestedThrust;
        bool useEngineResponseTime;
        float engineAccelerationSpeed;
        float engineDecelerationSpeed;
        bool useVelocityCurve;

        float thrustMultiplier;
        float thrustEfficiency;

        Vector3 com;
        Vector3 up, east, north;
        Vector3 thrustDirection;

        Vessel wireVessel;

        enum Mode
        {
            NONE,
            SET,
            THROTTLE,
            ACCELERATION,
            VELOCITY,
            CONTROLLED
        }

        Mode mode = Mode.NONE;
        float accuracy = 1f/1000000f;
        float setThrottle;
        float desiredThrottle;
        float desiredAcceleration;
        float desiredVelocity;

        float vesselMass;
        float maxAcceleration;
        float g;

        float heading;
        float pitch;
        float roll;

        bool useCamera = false;

        void FlyByWire(FlightCtrlState fcs)
        {
            if (mode != Mode.NONE)
            {
                fcs.mainThrottle = Mathf.Clamp01(setThrottle);
            }
        }

        Csv csv = null;

        int winId = 787628;
        Rect winRect = new Rect(150, 20, 400, 10);
        void OnGUI()
        {
            winRect = GUILayout.Window(winId, winRect, OnWindow, "JetEngineTesting", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        }
        void Show(string label, object value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            if (value == null) GUILayout.Label("(null)");
            else GUILayout.Label(value.ToString());
            GUILayout.EndHorizontal();
        }
        void Show()
        {
            GUILayout.Space(8);
        }
        void SetThrottle(float f)
        {
            GUI.enabled = !(mode==Mode.SET && setThrottle == f);
            if (GUILayout.Button(f.ToString("0%")))
            {
                mode = Mode.SET;
                setThrottle = f;
            }
            GUI.enabled = true;
        }
        void DesireThrottle(float f)
        {
            GUI.enabled = !(mode==Mode.THROTTLE && desiredThrottle == f);
            if (GUILayout.Button(f.ToString("0%")))
            {
                mode = Mode.THROTTLE;
                desiredThrottle = f;
            }
            GUI.enabled = true;
        }
        void SetAcceleration(float f)
        {
            GUI.enabled = !(mode == Mode.ACCELERATION && desiredAcceleration == f);
            if (GUILayout.Button((f > 0 ? "+" : "") + f.ToString("0.0")))
            {
                mode = Mode.ACCELERATION;
                desiredAcceleration = f;
            }
            GUI.enabled = true;
        }
        void SetVelocity(float f)
        {
            GUI.enabled = !(mode==Mode.VELOCITY && desiredVelocity == f);
            if (GUILayout.Button((f > 0 ? "+" : "") + f.ToString("0.0")))
            {
                mode = Mode.VELOCITY;
                desiredVelocity = f;
            }
            GUI.enabled = true;
        }
        void OnWindow(int id)
        {
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Set");
                GUILayout.FlexibleSpace();
                GUI.enabled = mode == Mode.SET;
                if (GUILayout.Button("OFF")) mode = Mode.NONE;
                SetThrottle(0.0f);
                SetThrottle(0.5f);
                SetThrottle(1.0f);
                GUILayout.EndHorizontal();
            }
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Desired");
                GUILayout.FlexibleSpace();
                GUI.enabled = mode == Mode.THROTTLE;
                if (GUILayout.Button("OFF")) mode = Mode.NONE;
                DesireThrottle(0.3f);
                DesireThrottle(0.5f);
                DesireThrottle(0.7f);
                GUILayout.EndHorizontal();
            }
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Acceleration");
                GUILayout.FlexibleSpace();
                GUI.enabled = mode == Mode.ACCELERATION;
                if (GUILayout.Button("OFF")) mode = Mode.NONE;
                SetAcceleration(-5.0f);
                SetAcceleration(0.0f);
                SetAcceleration(+5.0f);
                GUILayout.EndHorizontal();
            }
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Velocity");
                GUILayout.FlexibleSpace();
                GUI.enabled = mode == Mode.VELOCITY;
                if (GUILayout.Button("OFF")) mode = Mode.NONE;
                SetVelocity(-5.0f);
                SetVelocity(-0.5f);
                SetVelocity(0.0f);
                SetVelocity(+0.5f);
                SetVelocity(+5.0f);
                GUILayout.EndHorizontal();
            }
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Controlled");
                GUILayout.FlexibleSpace();
                GUI.enabled = mode == Mode.CONTROLLED;
                if (GUILayout.Button("OFF")) mode = Mode.NONE;
                GUI.enabled = mode != Mode.CONTROLLED;
                if (GUILayout.Button("ON")) mode = Mode.CONTROLLED;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Camera");
                GUILayout.FlexibleSpace();
                GUI.enabled = useCamera;
                if (GUILayout.Button("OFF")) useCamera = false;
                GUI.enabled = !useCamera;
                if (GUILayout.Button("ON")) useCamera = true;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Accuracy");
                GUILayout.FlexibleSpace();
                float.TryParse(GUILayout.TextField(accuracy.ToString()), out accuracy);
                GUILayout.EndHorizontal();
            }
            Show("vesselName", vesselName);
            if (vesselName != null)
            {
                Show("engineName", engineName);
                if (engineName != null)
                {
                    Show("currentThrottle", currentThrottle);
                    Show("requestedThrottle", requestedThrottle);
                }
            }
            Show();
            Show("desiredVelocity", desiredVelocity);
            Show("desiredAcceleration", desiredAcceleration);
            Show("desiredThrottle", desiredThrottle);
            Show();
            Show("heading", heading);
            Show("pitch", pitch);
            if (vessel!=null)
            {
                Show();
                var q = vessel.VesselSAS.lockedHeading;
                Show("SAS right", q * Vector3.right);
                Show("SAS up", q * Vector3.up);
                Show("SAS forward", q * Vector3.forward);
                Show("SAS euler", q.eulerAngles);
                Show("up", up);
                var n = Quaternion.LookRotation(q * Vector3.up, north);
                Show("n right", n * Vector3.right);
                Show("n up", n * Vector3.up);
                Show("n forward", n * Vector3.forward);
                Show("n euler", n.eulerAngles);

            }
            Show();
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = csv == null;
                if (GUILayout.Button("Record")) csv = new Csv();
                GUI.enabled = csv != null;
                if (GUILayout.Button("Save"))
                {
                    csv.Save("JetEngineTesting " + DateTime.Now.ToString("s").Replace(":", ".").Replace("T", " ") + ".csv");
                    csv = null;
                }
                if (GUILayout.Button("Clear")) csv = null;
                GUILayout.EndHorizontal();
            }
            GUI.DragWindow();
        }

        void Update()
        {
            if (vessel == null) return;
            if (useCamera && FlightCamera.fetch.mode == FlightCamera.Modes.FREE && !MapView.MapIsEnabled && !vessel.isEVA)
            {
                FlightCamera.fetch.camHdg = Mathf.Lerp(FlightCamera.fetch.camHdg, heading * Mathf.Deg2Rad, 0.5f * Time.deltaTime);
            }
        }

        float ControlMap(float f)
        {
            f = Mathf.Clamp01(f);
            return f*f*f;
        }

        void FixedUpdate()
        {
            time = Planetarium.GetUniversalTime();
            fixedDeltaTime = TimeWarp.fixedDeltaTime;

            vesselName = null;
            engineName = null;

            vessel = FlightGlobals.ActiveVessel;
            if (wireVessel != vessel)
            {
                if (wireVessel != null) wireVessel.OnFlyByWire -= FlyByWire;
                wireVessel = vessel;
                if (wireVessel != null) wireVessel.OnFlyByWire += FlyByWire;
            }
            if (vessel == null) return;
            vesselName = vessel.vesselName;
            var engine = vessel.GetActiveParts().SelectMany(part => part.Modules.OfType<ModuleEngines>()).FirstOrDefault();
            if (engine == null) return;
            engineName = engine.name;

            finalThrust = engine.finalThrust;
            fuelFlowGui = engine.fuelFlowGui;
            status = engine.status;
            statusL2 = engine.statusL2;
            thrustPercentage = engine.thrustPercentage;

            currentThrottle = engine.currentThrottle;
            EngineIgnited = engine.EngineIgnited;
            maxThrust = engine.maxThrust;
            minThrust = engine.minThrust;
            requestedThrottle = engine.requestedThrottle;
            requestedThrust = engine.requestedThrust;
            useEngineResponseTime = engine.useEngineResponseTime;
            engineAccelerationSpeed = engine.engineAccelerationSpeed;
            engineDecelerationSpeed = engine.engineDecelerationSpeed;
            useVelocityCurve = engine.useVelocityCurve;

            com = vessel.findWorldCenterOfMass();
            up = (com - vessel.mainBody.position).normalized;
            east = vessel.mainBody.getRFrmVel(com).normalized;
            north = -Vector3.Cross(up, east);

            var headingDir = vessel.GetSrfVelocity();
            headingDir -= Vector3.Dot(headingDir, up) * up;
            if (headingDir.sqrMagnitude > 0.5f)
            {
                headingDir = headingDir.normalized;
                heading = Mathf.Acos(Vector3.Dot(north, headingDir)) * Mathf.Rad2Deg;
                if (Vector3.Dot(east, headingDir) < 0) heading = -heading;
            }

            var pitchDir = vessel.GetSrfVelocity();
            if (pitchDir.sqrMagnitude > 0.5f)
            {
                pitchDir = pitchDir.normalized;
                pitch = Mathf.Acos(Vector3.Dot(up, pitchDir)) * Mathf.Rad2Deg;
            }

            //if (useCamera)
            //{
            //    var q = vessel.VesselSAS.lockedHeading;
            //    var forward = q * Vector3.up;
            //    q = Quaternion.LookRotation(forward, up);
            //    vessel.VesselSAS.LockHeading(q);
            //}

            thrustMultiplier = thrustPercentage / 100f;
            thrustEfficiency = 1;
            if (useVelocityCurve) thrustEfficiency *= engine.velocityCurve.Evaluate((float)vessel.srf_velocity.magnitude);

            up = (vessel.findWorldCenterOfMass() - vessel.mainBody.position).normalized;

            thrustDirection = Vector3.zero;
            foreach (var thrust in engine.thrustTransforms) thrustDirection += thrust.forward;
            if (engine.thrustTransforms.Count > 0) thrustDirection = thrustDirection.normalized;
            thrustEfficiency *= Vector3.Dot(-thrustDirection, up);

            //if (desire)
            //{
            //    desireCrank = Mathf.Round((desiredThrottle * thrustPercentage / 100f - currentThrottle) * 10000f) / 10000f;
            //    if (desireCrank > 0) desireCrank /= engineAccelerationSpeed * fixedDeltaTime;
            //    else if (desireCrank < 0) desireCrank /= engineDecelerationSpeed * fixedDeltaTime;
            //    if (currentThrottle + desireCrank >= 1) desireCrank = 1;
            //    else if (currentThrottle + desireCrank <= 0) desireCrank = 0;
            //    else if (desireCrank > 0) desireCrank = currentThrottle + desireCrank * desireCrank;
            //    else if (desireCrank < 0) desireCrank = currentThrottle - desireCrank * desireCrank;
            //    else desireCrank = desiredThrottle;
            //    desireCrank = Mathf.Clamp01(desireCrank * 100f / thrustPercentage);
            //}

            var deadzone = 0.01f;
            var ascend = Mathf.Clamp01((Input.GetAxis("joy0.9") - deadzone) / (1.0f - deadzone));
            var descend = Mathf.Clamp01((Input.GetAxis("joy0.8") - deadzone) / (1.0f - deadzone));
            if (ascend > 0 || descend > 0 || mode == Mode.CONTROLLED)
            {
                mode = Mode.CONTROLLED;
                desiredVelocity = (ControlMap(ascend) - ControlMap(descend)) * 30f;
            }

            if (mode == Mode.VELOCITY || mode == Mode.CONTROLLED)
            {
                desiredAcceleration = desiredVelocity - (float)vessel.verticalSpeed;
            }

            if (mode == Mode.VELOCITY || mode == Mode.ACCELERATION || mode == Mode.CONTROLLED)
            {
                vesselMass = vessel.GetTotalMass();
                maxAcceleration = maxThrust * thrustMultiplier * thrustEfficiency / vesselMass;
                g = (float) (vessel.mainBody.gravParameter / Math.Pow(vessel.altitude + vessel.mainBody.Radius, 2));
                desiredThrottle = (desiredAcceleration + g) / maxAcceleration;
            }

            if (mode == Mode.VELOCITY || mode == Mode.ACCELERATION || mode == Mode.THROTTLE || mode == Mode.CONTROLLED)
            {
                var nextThrottle = setThrottle * thrustMultiplier - currentThrottle;
                if (nextThrottle > 0f) nextThrottle *= engineAccelerationSpeed * fixedDeltaTime;
                else if (nextThrottle < 0f) nextThrottle *= engineDecelerationSpeed * fixedDeltaTime;
                nextThrottle += currentThrottle;
                setThrottle = Mathf.Round((desiredThrottle * thrustMultiplier - nextThrottle) / accuracy) * accuracy;
                if (setThrottle > 0f) setThrottle /= engineAccelerationSpeed * fixedDeltaTime;
                else if (setThrottle < 0f) setThrottle /= engineDecelerationSpeed * fixedDeltaTime;
                if (setThrottle == 0f) setThrottle = desiredThrottle * thrustMultiplier;
                else setThrottle += nextThrottle;
                setThrottle = Mathf.Clamp01(setThrottle / thrustMultiplier);
            }

            if (csv != null)
            {
                csv.Add("time",time);
                csv.Add("fixedDeltaTime",fixedDeltaTime);

                csv.Add("vesselName",vesselName);
                csv.Add("engineName",engineName);

                csv.Add("finalThrust",finalThrust);
                csv.Add("fuelFlowGui",fuelFlowGui);
                csv.Add("status",status);
                csv.Add("statusL2",statusL2);
                csv.Add("thrustPercentage",thrustPercentage);

                csv.Add("currentThrottle",currentThrottle);
                csv.Add("EngineIgnited",EngineIgnited);
                csv.Add("maxThrust",maxThrust);
                csv.Add("minThrust",minThrust);
                csv.Add("requestedThrottle",requestedThrottle);
                csv.Add("requestedThrust",requestedThrust);
                csv.Add("useEngineResponseTime",useEngineResponseTime);
                csv.Add("engineAccelerationSpeed",engineAccelerationSpeed);
                csv.Add("engineDecelerationSpeed",engineDecelerationSpeed);
                csv.Add("useVelocityCurve",useVelocityCurve);

                csv.Add("mode", mode);
                csv.Add("setThrottle", setThrottle);
                csv.Add("desiredThrottle", desiredThrottle);
                csv.Add("desiredAcceleration", desiredAcceleration);
                csv.Add("desiredVelocity", desiredVelocity);

                csv.Add("vesselMass", vesselMass);
                csv.Add("maxAcceleration", maxAcceleration);
                csv.Add("g", g);

                csv.Add("thrustMultiplier", thrustMultiplier);
                csv.Add("thrustEfficiency", thrustEfficiency);

                csv.Row();
            }

        }

    }
}
