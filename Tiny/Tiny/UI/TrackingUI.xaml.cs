﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
using KinectSerializer;
using Microsoft.Kinect;
using System.Diagnostics;
using System.Net;
using SkeletonVis = Tiny.UI.SkeletonVisualizer;
using Tiny.WorldView;

namespace Tiny.UI
{
    public partial class TrackingUI : Window, INotifyPropertyChanged
    {
        public class JointSketch
        {
            public CameraSpacePoint Coordinate { get; private set; }
            public TrackingState TrackingState { get; private set; }

            public JointSketch(CameraSpacePoint coordinate, TrackingState trackingState)
            {
                this.Coordinate = coordinate;
                this.TrackingState = trackingState;
            }
        }

        public class SkeletonSketch
        {
            public Dictionary<JointType, JointSketch> Joints { get; private set; }

            public SkeletonSketch(Dictionary<JointType, JointSketch> joints) {
                this.Joints = joints;
            }
        }

        public class PersonSketch
        {
            public Pen Pen { get; private set; }
            public IEnumerable<SkeletonSketch> SkeletonSketches { get; private set; }

            public PersonSketch(Pen pen, IEnumerable<SkeletonSketch> skeletonSketches)
            {
                this.Pen = pen;
                this.SkeletonSketches = skeletonSketches;
            }
        }

        private Dictionary<string, MenuItem> referenceKinectIPs;
        private string currentReferenceKinectIP;
        private bool showAllFOV;
        private readonly string ViewMode_All = "All";
        private readonly string ViewMode_Average = "Average";
        private string trackingStatusText;

        private DrawingGroup bodyDrawingGroup;
        private DrawingImage bodyImageSource;
        private List<Pen> personColors;

        private KinectSensor kinectSensor;
        private CoordinateMapper coordinateMapper;

        public TrackingUI()
        {
            InitializeComponent();
            this.DataContext = this;

            this.referenceKinectIPs = new Dictionary<string, MenuItem>();
            this.currentReferenceKinectIP = "";
            this.showAllFOV = true;
            this.TrackingStatusText = Properties.Resources.TRACKING_CALIBRATION;
            
            this.bodyDrawingGroup = new DrawingGroup();
            this.bodyImageSource = new DrawingImage(this.bodyDrawingGroup);
            // A person will have the same color
            this.personColors = new List<Pen>();
            this.personColors.Add(new Pen(Brushes.Red, 6));
            this.personColors.Add(new Pen(Brushes.Orange, 6));
            this.personColors.Add(new Pen(Brushes.Green, 6));
            this.personColors.Add(new Pen(Brushes.Blue, 6));
            this.personColors.Add(new Pen(Brushes.Indigo, 6));
            this.personColors.Add(new Pen(Brushes.Violet, 6));

            this.kinectSensor = KinectSensor.GetDefault();
            this.kinectSensor.Open();
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ImageSource BodyStreamImageSource
        {
            get
            {
                return this.bodyImageSource;
            }
        }

        public string TrackingStatusText
        {
            get
            {
                return this.trackingStatusText;
            }
            set
            {
                if (this.trackingStatusText != value)
                {
                    this.trackingStatusText = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("TrackingStatusText"));
                    }
                }
            }
        }

        public void setCalibrationComplete(bool completed)
        {
            this.TrackingStatusText = completed ? Properties.Resources.TRACKING_START
                                                            : Properties.Resources.TRACKING_CALIBRATION;
        }

        public void UpdateDisplay(Tracker.Result result)
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                //this.UpdateReferenceKinectMenu(result.FOVs);
                this.DisplayBodyFrames(result);
            }));
        }

        private Tracker.Result.KinectFOV GetReferenceKinectFOV(IEnumerable<Tracker.Result.KinectFOV> fovs)
        {
            Tracker.Result.KinectFOV referenceFOV = fovs.First();
            foreach (Tracker.Result.KinectFOV fov in fovs)
            {
                if (fov.ClientIP.ToString().Equals(this.currentReferenceKinectIP))
                {
                    referenceFOV = fov;
                }
            }
            return referenceFOV;
        }

        private void DisplayBodyFrames(Tracker.Result result)
        {
            if (!result.People.Any())
            {
                return;
            }
            Tracker.Result.KinectFOV referenceFOV = this.GetReferenceKinectFOV(result.FOVs);
            KinectCamera.Dimension referenceDim = referenceFOV.Dimension;
            int frameWidth = referenceDim.DepthFrameWidth;
            int frameHeight = referenceDim.DepthFrameHeight;
            using (DrawingContext dc = this.bodyDrawingGroup.Open())
            {
                SkeletonVis.DrawBackground(frameWidth, frameHeight, dc);
            }
            int personIdx = 0;
            foreach (Tracker.Result.Person person in result.People)
            {
                TrackingSkeleton referenceSkeleton = person.FindSkeletonInFOV(referenceFOV);
                List<SkeletonSketch> skeletonSketches = new List<SkeletonSketch>();
                foreach (Tracker.Result.SkeletonMatch match in person.SkeletonMatches)
                {
                    WBody body = match.Skeleton.CurrentPosition.Worldview;
                    KinectBody kinectBody = WBody.TransformToKinectBody(body, referenceSkeleton.InitialAngle, referenceSkeleton.InitialPosition);

                    Dictionary<JointType, JointSketch> jointSketches = new Dictionary<JointType, JointSketch>();
                    foreach (JointType jt in kinectBody.Joints.Keys)
                    {
                        jointSketches[jt] = new JointSketch(kinectBody.Joints[jt], body.Joints[jt].TrackingState);
                    }
                    skeletonSketches.Add(new SkeletonSketch(jointSketches));
                }
                Pen pen = this.personColors[personIdx++];
                PersonSketch personSketch = new PersonSketch(pen, skeletonSketches);
                if (this.showAllFOV)
                {
                    this.DrawAllSkeletons(personSketch);
                }
                else
                {
                    this.DrawAverageSkeletons(personSketch);
                }
            }
            SkeletonVis.DrawClipRegion(frameWidth, frameHeight, this.bodyDrawingGroup);
        }

        private void DrawAllSkeletons(PersonSketch personSketch)
        {
            foreach (SkeletonSketch skeletonSketch in personSketch.SkeletonSketches)
            {
                Dictionary<JointType, Tuple<Point, TrackingState>> joints = new Dictionary<JointType, Tuple<Point, TrackingState>>();
                foreach (JointType jt in skeletonSketch.Joints.Keys)
                {
                    TrackingState trackingState = skeletonSketch.Joints[jt].TrackingState;
                    CameraSpacePoint position = skeletonSketch.Joints[jt].Coordinate;
                    if (position.Z < 0)
                    {
                        position.Z = 0.1f;
                    }
                    DepthSpacePoint dsPt = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                    joints[jt] = Tuple.Create(new Point(dsPt.X, dsPt.Y), trackingState);
                }
                using (DrawingContext dc = this.bodyDrawingGroup.Open())
                {
                    SkeletonVis.DrawBody(joints, dc, personSketch.Pen);
                }
            }
        }

        private void DrawAverageSkeletons(PersonSketch personSketch)
        {
            Dictionary<JointType, CameraSpacePoint> sumJoints = new Dictionary<JointType,CameraSpacePoint>();
            Dictionary<JointType, int> jointsCount = new Dictionary<JointType, int>();
            foreach (SkeletonSketch skeletonSketch in personSketch.SkeletonSketches)
            {
                foreach (JointType jt in skeletonSketch.Joints.Keys)
                {
                    if (sumJoints.ContainsKey(jt))
                    {
                        CameraSpacePoint accumulatePt = new CameraSpacePoint();
                        accumulatePt.X = sumJoints[jt].X + skeletonSketch.Joints[jt].Coordinate.X;
                        accumulatePt.Y = sumJoints[jt].Y + skeletonSketch.Joints[jt].Coordinate.Y;
                        accumulatePt.Z = sumJoints[jt].Z + skeletonSketch.Joints[jt].Coordinate.Z;
                        sumJoints[jt] = accumulatePt;
                        jointsCount[jt] += 1;
                    }
                    else
                    {
                        sumJoints[jt] = skeletonSketch.Joints[jt].Coordinate;
                        jointsCount[jt] = 1;
                    }
                }
            }
            Dictionary<JointType, Tuple<Point, TrackingState>> joints = new Dictionary<JointType, Tuple<Point, TrackingState>>();
            foreach (JointType jt in sumJoints.Keys)
            {
                CameraSpacePoint position = new CameraSpacePoint();
                position.X = sumJoints[jt].X / jointsCount[jt];
                position.Y = sumJoints[jt].Y / jointsCount[jt];
                position.Z = sumJoints[jt].Y / jointsCount[jt];
                if (position.Z < 0)
                {
                    position.Z = 0.1f;
                }
                DepthSpacePoint dsPt = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                joints[jt] = Tuple.Create(new Point(dsPt.X, dsPt.Y), TrackingState.Tracked);
            }
            using (DrawingContext dc = this.bodyDrawingGroup.Open())
            {
                SkeletonVis.DrawBody(joints, dc, personSketch.Pen);
            }
        }

        private void ReferenceKinectButton_Click(object sender, RoutedEventArgs e)
        {
            Button referenceKinectBtn = sender as Button;
            referenceKinectBtn.ContextMenu.IsEnabled = true;
            referenceKinectBtn.ContextMenu.PlacementTarget = referenceKinectBtn;
            referenceKinectBtn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            referenceKinectBtn.ContextMenu.IsOpen = true;
        }

        internal void AddKinectCamera(IPEndPoint clientIP)
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                MenuItem kinectIPItem = new MenuItem();
                kinectIPItem.Header = clientIP.ToString();
                kinectIPItem.Click += ReferenceKinectItem_Click;
                this.ReferenceKinectMenu.Items.Add(kinectIPItem);
                this.referenceKinectIPs[clientIP.ToString()] = kinectIPItem;
            }));
        }

        internal void RemoveKinectCamera(IPEndPoint clientIP)
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                this.ReferenceKinectMenu.Items.Remove(this.referenceKinectIPs[clientIP.ToString()]);
                this.referenceKinectIPs.Remove(clientIP.ToString());
                if (this.currentReferenceKinectIP.Equals(clientIP))
                {
                    this.ReferenceKinectBtn.Content = "Reference Kinect";
                    this.currentReferenceKinectIP = "";
                }
            }));
        }

        private void ReferenceKinectItem_Click(object sender, RoutedEventArgs e)
        {
            string referenceKinectIP = (sender as MenuItem).Header.ToString();
            this.currentReferenceKinectIP = referenceKinectIP;
            this.ReferenceKinectBtn.Content = referenceKinectIP;
        }

        private void ViewModeBtn_Click(object sender, RoutedEventArgs e)
        {
            Button viewModeBtn = sender as Button;
            viewModeBtn.ContextMenu.IsEnabled = true;
            viewModeBtn.ContextMenu.PlacementTarget = viewModeBtn;
            viewModeBtn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            viewModeBtn.ContextMenu.IsOpen = true;
        }

        private void ViewMode_All_Click(object sender, RoutedEventArgs e)
        {
            this.showAllFOV = true;
            this.ViewModeBtn.Content = this.ViewMode_All;
        }

        private void ViewMode_Average_Click(object sender, RoutedEventArgs e)
        {
            this.showAllFOV = false;
            this.ViewModeBtn.Content = this.ViewMode_Average;
        }
    }
}
