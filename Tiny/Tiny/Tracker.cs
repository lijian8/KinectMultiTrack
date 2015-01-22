﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KinectSerializer;
using System.Collections.Concurrent;
using System.Net;
using System.Diagnostics;
using Microsoft.Kinect;
using System.Runtime.CompilerServices;

namespace Tiny
{
    class Tracker
    {
        public const int CALIBRATION_FRAMES = 120;
        private bool calibrated = false;

        private readonly int KINECT_COUNT;
        private ConcurrentDictionary<IPEndPoint, KinectAgent> kinectsDict;

        private readonly object syncFrameLock = new object();

        public class Result
        {
            public class KinectFOV
            {
                private IPEndPoint clientIP;
                private KinectAgent.Dimension dimension;
                private IEnumerable<Person> people;

                public IPEndPoint ClientIP
                {
                    get
                    {
                        return this.clientIP;
                    }
                }

                public KinectAgent.Dimension Dimension
                {
                    get
                    {
                        return this.dimension;
                    }
                }

                public IEnumerable<Person> People
                {
                    get
                    {
                        return this.people;
                    }
                }

                public KinectFOV(IPEndPoint clientIP, KinectAgent.Dimension dimension, IEnumerable<Person> people)
                {
                    this.clientIP = clientIP;
                    this.dimension = dimension;
                    this.people = people;
                }
            }

            private IEnumerable<KinectFOV> fovs;
            private IEnumerable<IEnumerable<Person>> matches;

            public IEnumerable<KinectFOV> FOVs
            {
                get
                {
                    return this.fovs;
                }
            }

            public IEnumerable<IEnumerable<Person>> Matches
            {
                get
                {
                    return this.matches;
                }
            }

            public Result(IEnumerable<KinectFOV> fovs, IEnumerable<IEnumerable<Person>> matches)
            {
                this.fovs = fovs;
                this.matches = matches;
            }
        }

        public Tracker(int kinectCount)
        {
            this.KINECT_COUNT = kinectCount;
            this.kinectsDict = new ConcurrentDictionary<IPEndPoint, KinectAgent>();
        }

        public void RemoveClient(IPEndPoint clientIP)
        {
            KinectAgent kinect;
            if (this.kinectsDict.TryRemove(clientIP, out kinect))
            {
                kinect.DisposeUI();
            }
        }

        private bool RequireCalibration()
        {
            if (this.kinectsDict.Count < this.KINECT_COUNT && this.calibrated)
            {
                return false;
            }
            else
            {
                foreach (KinectAgent kinect in this.kinectsDict.Values)
                {
                    if (kinect.UnprocessedFramesCount < Tracker.CALIBRATION_FRAMES)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public Result Synchronize(IPEndPoint clientIP, SBodyFrame bodyframe)
        {
            if (!this.kinectsDict.ContainsKey(clientIP))
            {
                this.kinectsDict[clientIP] = new KinectAgent();
            }
            lock (syncFrameLock)
            {
                if (this.RequireCalibration())
                {
                    foreach (KinectAgent kinect in this.kinectsDict.Values)
                    {
                        kinect.Calibrate();
                    }
                }
                // Get a copy of the current positions of users
                this.kinectsDict[clientIP].ProcessFrames(bodyframe);
                List<Result.KinectFOV> fovs = new List<Result.KinectFOV>();
                foreach (IPEndPoint kinectId in this.kinectsDict.Keys)
                {
                    KinectAgent.Dimension dimension = this.kinectsDict[kinectId].FrameDimension;
                    IEnumerable<Person> people = this.kinectsDict[kinectId].People;
                    fovs.Add(new Result.KinectFOV(kinectId, dimension, people));
                }
                IEnumerable<IEnumerable<Person>> matches = this.MatchPeople(fovs);
                return new Tracker.Result(fovs, matches);
            }
        }

        private IEnumerable<IEnumerable<Person>> MatchPeople(IEnumerable<Result.KinectFOV> fovs)
        {
            Debug.WriteLine("Matching skeleton from different FOVs...");
            List<List<Person>> matches = new List<List<Person>>();
            foreach (Result.KinectFOV fov in fovs)
            {
                Debug.WriteLine("FOV: " + fov.ClientIP + " People: " + Helper.Count(fov.People);
                foreach (Person person in fov.People)
                {
                    
                }
            }
            return matches;
        }

        private Person FindClosestPersonByDistance(IEnumerable<Result.KinectFOV> fovs, Result.KinectFOV targetFOV, Person targetPerson)
        {
            Person suspect;
            foreach (Result.KinectFOV )
        }
    }
}