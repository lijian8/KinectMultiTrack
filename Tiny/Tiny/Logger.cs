﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;
using Tiny.WorldView;
using KinectSerializer;
using System.Diagnostics;
using System.Collections.Specialized;
using System.IO;

namespace Tiny
{
    public class Logger
    {
        public static readonly int NA = -1;
        public static readonly int ALL = 0;
        public static readonly int STATIONARY = 1;
        public static readonly int WALK = 2;

        private static readonly string STUDY = "Study";
        private static readonly string SCENARIO = "Scenario";
        private static readonly string TRACKER_TIME = "Tracker_Time";
        private static readonly string PERSON = "Person#";
        private static readonly string SKEL = "Skeleton#";
        private static readonly string SKEL_TIME = "Skeleton_Time";
        private static readonly string SKEL_INIT_ANGLE = "Skeleton_Init_Angle";
        private static readonly string SKEL_INIT_DIST = "Skeleton_Init_Dist";
        private static readonly string KINECT = "Kinect#";
        private static readonly string KINECT_TILT_ANGLE = "Kinect_Tilt_Angle";
        private static readonly string KINECT_HEIGHT = "Kinect_Height";
        private static readonly string X = "X";
        private static readonly string Y = "Y";
        private static readonly string Z = "Z";

        public static int CURRENT_STUDY = Logger.NA;
        public static int CURRENT_SCENARIO = Logger.NA;

        private static readonly string FILE_DIR = "..\\..\\..\\..\\Logs\\";
        private static readonly string FILE_NAME_FORMAT = "Raw_Study_{0}_Scenario_{1}.csv";
        private static readonly List<string> HEADERS_RAW = Logger.GetHeaders();
        private static readonly StreamWriter WRITER_RAW = Logger.OpenFileWriter(Logger.FILE_DIR, Logger.HEADERS_RAW);

        private static readonly object WRITE_LOCK = new object();

        private static List<string> GetHeaders()
        {
            List<string> headers = new List<string>() { 
                Logger.STUDY,
                Logger.SCENARIO,
                Logger.TRACKER_TIME, 
                Logger.PERSON,
                Logger.SKEL,
                Logger.SKEL_INIT_ANGLE,
                Logger.SKEL_INIT_DIST,
                Logger.SKEL_TIME,
                Logger.KINECT,
                Logger.KINECT_TILT_ANGLE,
                Logger.KINECT_HEIGHT
            };
            Logger.AddJointHeaders(ref headers);
            return headers;
        }

        private static void AddJointHeaders(ref List<string> headers)
        {
            foreach (JointType jt in SkeletonStructure.Joints)
            {
                string x = jt.ToString() + "_" + Logger.X;
                string y = jt.ToString() + "_" + Logger.Y;
                string z = jt.ToString() + "_" + Logger.Z;
                headers.Add(x);
                headers.Add(y);
                headers.Add(z);
            }
        }

        private static StreamWriter OpenFileWriter(string directory, List<string> headers)
        {
            string filepath = directory + String.Format(Logger.FILE_NAME_FORMAT, Logger.CURRENT_STUDY, Logger.CURRENT_SCENARIO);
            Logger.CreateFile(filepath, headers);
            return new StreamWriter(filepath, true);
        }

        private static void CreateFile(string filepath, List<string> headers)
        {
            using (StreamReader r = new StreamReader(File.Open(filepath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite)))
            {
                string header = r.ReadLine();
                if (header == null)
                {
                    using (StreamWriter w = new StreamWriter(filepath, true))
                    {
                        string prefix = "";
                        foreach (string h in headers)
                        {
                            w.Write(prefix + h);
                            prefix = ", ";
                        }
                        w.WriteLine();
                    }
                }
            }
        }

        public static void Write(TrackerResult result)
        {
            lock (WRITE_LOCK)
            {   
                foreach (TrackerResult.Person person in result.People)
                {
                    TrackerResult.PotentialSkeleton reference = TrackerResult.GetLocalSkeletonReference(person);
                    double referenceAngle = reference.Skeleton.InitialAngle;
                    WCoordinate referencePosition = reference.Skeleton.InitialCenterPosition;
                    List<Tuple<TrackerResult.PotentialSkeleton, Dictionary<JointType, KinectJoint>>> skeletonCoordinates = new List<Tuple<TrackerResult.PotentialSkeleton, Dictionary<JointType, KinectJoint>>>();
                    foreach (TrackerResult.PotentialSkeleton skeleton in person.Skeletons)
                    {
                        KinectBody body = WBody.TransformWorldToKinectBody(skeleton.Skeleton.CurrentPosition.Worldview, referenceAngle, referencePosition);
                        skeletonCoordinates.Add(Tuple.Create(skeleton, body.Joints));
                    }
                    // Raw
                    Logger.WriteData(Logger.WRITER_RAW, result.Timestamp, person.Id, skeletonCoordinates);
                }
            }
        }

        private static void WriteData(StreamWriter writer, long timestamp, uint personId, IEnumerable<Tuple<TrackerResult.PotentialSkeleton, Dictionary<JointType, KinectJoint>>> coordinates)
        {
            foreach (Tuple<TrackerResult.PotentialSkeleton, Dictionary<JointType, KinectJoint>> coordinateTuple in coordinates)
            {
                TrackerResult.PotentialSkeleton replica = coordinateTuple.Item1;
                Dictionary<JointType, KinectJoint> joints = coordinateTuple.Item2;
                // Headers
                writer.Write(String.Format("{0}, {1}, {2}, {3}, ", Logger.CURRENT_STUDY, Logger.CURRENT_SCENARIO, timestamp, personId));
                writer.Write(String.Format("{0}, {1}, {2}, {3}, ", replica.Id, replica.Skeleton.InitialAngle, replica.Skeleton.InitialDistance, replica.Skeleton.Timestamp));
                writer.Write(String.Format("{0}, {1}, {2}", replica.FOV.Id, replica.FOV.Specification.TiltAngle, replica.FOV.Specification.Height));
                // Joint_X, Joint_Y, Joint_Z
                Logger.WriteJointsData(writer, joints);
            }
        }

        private static void WriteJointsData(StreamWriter writer, Dictionary<JointType, KinectJoint> joints)
        {
            // Joint_X, Joint_Y, Joint_Z
            string prefix = ", ";
            foreach (JointType jt in SkeletonStructure.Joints)
            {
                writer.Write(prefix);
                if (joints.ContainsKey(jt))
                {
                    writer.Write(String.Format("{0}, {1}, {2}", joints[jt].Position.X, joints[jt].Position.Y, joints[jt].Position.Z));
                }
                else
                {
                    writer.Write(String.Format("{0}, {1}, {2}", Logger.NA, Logger.NA, Logger.NA));
                }
            }
            // newline
            writer.WriteLine();
        }

        public static void Flush()
        {
            Logger.WRITER_RAW.Flush();
        }

        public static void Close()
        {
            Logger.WRITER_RAW.Close();
        }
    }
}
