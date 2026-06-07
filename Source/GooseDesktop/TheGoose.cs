using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using SamEngine;

namespace GooseDesktop
{
    internal static class TheGoose
    {
        public static void Init()
        {
            TheGoose.position = new Vector2(-20f, 120f);
            TheGoose.targetPos = new Vector2(100f, 150f);
            if (!GooseConfig.settings.CanAttackAtRandom)
            {
                int num = Array.IndexOf<int>(TheGoose.taskPickerDeck.indices, Array.IndexOf<TheGoose.GooseTask>(TheGoose.gooseTaskWeightedList, TheGoose.GooseTask.CollectWindow_Meme));
                int num2 = TheGoose.taskPickerDeck.indices[0];
                TheGoose.taskPickerDeck.indices[0] = TheGoose.taskPickerDeck.indices[num];
                TheGoose.taskPickerDeck.indices[num] = num2;
            }
            TheGoose.lFootPos = TheGoose.GetFootHome(false);
            TheGoose.rFootPos = TheGoose.GetFootHome(true);
            TheGoose.shadowBitmap = new Bitmap(2, 2);
            TheGoose.shadowBitmap.SetPixel(0, 0, Color.Transparent);
            TheGoose.shadowBitmap.SetPixel(1, 1, Color.Transparent);
            TheGoose.shadowBitmap.SetPixel(1, 0, Color.Transparent);
            TheGoose.shadowBitmap.SetPixel(0, 1, Color.DarkGray);
            TheGoose.shadowBrush = new TextureBrush(TheGoose.shadowBitmap);
            TheGoose.shadowPen = new Pen(TheGoose.shadowBrush);
            Pen pen = TheGoose.shadowPen;
            TheGoose.shadowPen.EndCap = LineCap.Round;
            pen.StartCap = LineCap.Round;
            TheGoose.DrawingPen = new Pen(Brushes.White);
            Pen drawingPen = TheGoose.DrawingPen;
            TheGoose.DrawingPen.StartCap = LineCap.Round;
            drawingPen.EndCap = LineCap.Round;
            TheGoose.SetTask(TheGoose.GooseTask.Wander);
        }

        private static void SetSpeed(TheGoose.SpeedTiers tier)
        {
            Logger.LogMethod();
            switch (tier)
            {
                case TheGoose.SpeedTiers.Walk:
                    TheGoose.currentSpeed = 80f;
                    TheGoose.currentAcceleration = 1300f;
                    TheGoose.stepTime = 0.2f;
                    return;
                case TheGoose.SpeedTiers.Run:
                    TheGoose.currentSpeed = 200f;
                    TheGoose.currentAcceleration = 1300f;
                    TheGoose.stepTime = 0.2f;
                    return;
                case TheGoose.SpeedTiers.Charge:
                    TheGoose.currentSpeed = 400f;
                    TheGoose.currentAcceleration = 2300f;
                    TheGoose.stepTime = 0.1f;
                    return;
                default:
                    return;
            }
        }

        public static void Tick()
        {
            Logger.LogMethod();
            Cursor.Clip = Rectangle.Empty;
            if (TheGoose.currentTask != TheGoose.GooseTask.NabMouse && (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left && !TheGoose.lastFrameMouseButtonPressed && Vector2.Distance(TheGoose.position + new Vector2(0f, 14f), new Vector2((float)Cursor.Position.X, (float)Cursor.Position.Y)) < 30f)
            {
                TheGoose.SetTask(TheGoose.GooseTask.NabMouse);
            }
            TheGoose.lastFrameMouseButtonPressed = ((Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left);
            TheGoose.targetDirection = Vector2.Normalize(TheGoose.targetPos - TheGoose.position);
            TheGoose.overrideExtendNeck = false;
            TheGoose.RunAI();
            Vector2 vector = Vector2.Lerp(Vector2.GetFromAngleDegrees(TheGoose.direction), TheGoose.targetDirection, 0.25f);
            TheGoose.direction = (float)Math.Atan2((double)vector.y, (double)vector.x) * 57.2957764f;
            if (Vector2.Magnitude(TheGoose.velocity) > TheGoose.currentSpeed)
            {
                TheGoose.velocity = Vector2.Normalize(TheGoose.velocity) * TheGoose.currentSpeed;
            }
            TheGoose.velocity += Vector2.Normalize(TheGoose.targetPos - TheGoose.position) * TheGoose.currentAcceleration * 0.008333334f;
            TheGoose.position += TheGoose.velocity * 0.008333334f;
            TheGoose.SolveFeet();
            Vector2.Magnitude(TheGoose.velocity);
            int num = (TheGoose.overrideExtendNeck | TheGoose.currentSpeed >= 200f) ? 1 : 0;
            TheGoose.gooseRig.neckLerpPercent = SamMath.Lerp(TheGoose.gooseRig.neckLerpPercent, (float)num, 0.075f);
        }
        private static void RunWander()
        {
            Logger.LogMethod();
            if (Time.time - TheGoose.taskWanderInfo.wanderingStartTime > TheGoose.taskWanderInfo.wanderingDuration)
            {
                TheGoose.ChooseNextTask();
                return;
            }
            if (TheGoose.taskWanderInfo.pauseStartTime > 0f)
            {
                if (Time.time - TheGoose.taskWanderInfo.pauseStartTime > TheGoose.taskWanderInfo.pauseDuration)
                {
                    TheGoose.taskWanderInfo.pauseStartTime = -1f;
                    float num = TheGoose.Task_Wander.GetRandomWalkTime() * TheGoose.currentSpeed;
                    TheGoose.targetPos = new Vector2(SamMath.RandomRange(0f, (float)Program.mainForm.Width), SamMath.RandomRange(0f, (float)Program.mainForm.Height));
                    if (Vector2.Distance(TheGoose.position, TheGoose.targetPos) > num)
                    {
                        TheGoose.targetPos = TheGoose.position + Vector2.Normalize(TheGoose.targetPos - TheGoose.position) * num;
                    }
                    return;
                }
                TheGoose.velocity = Vector2.zero;
                return;
            }
            else
            {
                if (Vector2.Distance(TheGoose.position, TheGoose.targetPos) < 20f)
                {
                    TheGoose.taskWanderInfo.pauseStartTime = Time.time;
                    TheGoose.taskWanderInfo.pauseDuration = TheGoose.Task_Wander.GetRandomPauseDuration();
                    return;
                }
                return;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static void RunNabMouse()
        {
            Vector2 vector = new Vector2((float)Cursor.Position.X, (float)Cursor.Position.Y);
            Vector2 head2EndPoint = TheGoose.gooseRig.head2EndPoint;
            if (TheGoose.taskNabMouseInfo.currentStage == TheGoose.Task_NabMouse.Stage.SeekingMouse)
            {
                TheGoose.SetSpeed(TheGoose.SpeedTiers.Charge);
                TheGoose.targetPos = vector - (TheGoose.gooseRig.head2EndPoint - TheGoose.position);
                if (Vector2.Distance(head2EndPoint, vector) < 15f)
                {
                    TheGoose.taskNabMouseInfo.originalVectorToMouse = vector - head2EndPoint;
                    TheGoose.taskNabMouseInfo.grabbedOriginalTime = Time.time;
                    TheGoose.taskNabMouseInfo.dragToPoint = TheGoose.position;
                    while (Vector2.Distance(TheGoose.taskNabMouseInfo.dragToPoint, TheGoose.position) / 400f < 1.2f)
                    {
                        TheGoose.taskNabMouseInfo.dragToPoint = new Vector2((float)SamMath.Rand.NextDouble() * (float)Program.mainForm.Width, (float)SamMath.Rand.NextDouble() * (float)Program.mainForm.Height);
                    }
                    TheGoose.targetPos = TheGoose.taskNabMouseInfo.dragToPoint;
                    TheGoose.SetForegroundWindow(Program.mainForm.Handle);
                    Sound.CHOMP();
                    TheGoose.taskNabMouseInfo.currentStage = TheGoose.Task_NabMouse.Stage.DraggingMouseAway;
                }
                if (Time.time > TheGoose.taskNabMouseInfo.chaseStartTime + 9f)
                {
                    TheGoose.taskNabMouseInfo.currentStage = TheGoose.Task_NabMouse.Stage.Decelerating;
                }
            }
            if (TheGoose.taskNabMouseInfo.currentStage == TheGoose.Task_NabMouse.Stage.DraggingMouseAway)
            {
                if (Vector2.Distance(TheGoose.position, TheGoose.targetPos) < 30f)
                {
                    Cursor.Clip = Rectangle.Empty;
                    TheGoose.taskNabMouseInfo.currentStage = TheGoose.Task_NabMouse.Stage.Decelerating;
                }
                else
                {
                    float p = Math.Min((Time.time - TheGoose.taskNabMouseInfo.grabbedOriginalTime) / 0.06f, 1f);
                    Vector2 vector2 = Vector2.Lerp(TheGoose.taskNabMouseInfo.originalVectorToMouse, TheGoose.Task_NabMouse.StruggleRange, p);
                    TheGoose.tmpRect.Location = TheGoose.ToIntPoint(new Vector2
                    {
                        x = ((vector2.x < 0f) ? (head2EndPoint.x + vector2.x) : head2EndPoint.x),
                        y = ((vector2.y < 0f) ? (head2EndPoint.y + vector2.y) : head2EndPoint.y)
                    });
                    TheGoose.tmpSize.Width = Math.Abs((int)vector2.x);
                    TheGoose.tmpSize.Height = Math.Abs((int)vector2.y);
                    TheGoose.tmpRect.Size = TheGoose.tmpSize;
                    Cursor.Clip = TheGoose.tmpRect;
                }
            }
            if (TheGoose.taskNabMouseInfo.currentStage == TheGoose.Task_NabMouse.Stage.Decelerating)
            {
                TheGoose.targetPos = TheGoose.position + Vector2.Normalize(TheGoose.velocity) * 5f;
                TheGoose.velocity -= Vector2.Normalize(TheGoose.velocity) * TheGoose.currentAcceleration * 2f * 0.008333334f;
                if (Vector2.Magnitude(TheGoose.velocity) < 80f)
                {
                    TheGoose.SetTask(TheGoose.GooseTask.Wander);
                }
            }
        }

        private static void RunCollectWindow()
        {
            Logger.LogMethod();
            switch (TheGoose.taskCollectWindowInfo.stage)
            {
                case TheGoose.Task_CollectWindow.Stage.WalkingOffscreen:
                    if (Vector2.Distance(TheGoose.position, TheGoose.targetPos) < 5f)
                    {
                        TheGoose.taskCollectWindowInfo.secsToWait = TheGoose.Task_CollectWindow.GetWaitTime();
                        TheGoose.taskCollectWindowInfo.waitStartTime = Time.time;
                        TheGoose.taskCollectWindowInfo.stage = TheGoose.Task_CollectWindow.Stage.WaitingToBringWindowBack;
                        return;
                    }
                    break;
                case TheGoose.Task_CollectWindow.Stage.WaitingToBringWindowBack:
                    if (Time.time - TheGoose.taskCollectWindowInfo.waitStartTime > TheGoose.taskCollectWindowInfo.secsToWait)
                    {
                        TheGoose.taskCollectWindowInfo.mainForm.FormClosing += TheGoose.CollectMemeTask_CancelEarly;
                        new Thread(delegate ()
                        {
                            TheGoose.taskCollectWindowInfo.mainForm.ShowDialog();
                        }).Start();
                        switch (TheGoose.taskCollectWindowInfo.screenDirection)
                        {
                            case TheGoose.Task_CollectWindow.ScreenDirection.Left:
                                TheGoose.targetPos.y = SamMath.Lerp(TheGoose.position.y, (float)(Program.mainForm.Height / 2), SamMath.RandomRange(0.2f, 0.3f));
                                TheGoose.targetPos.x = (float)TheGoose.taskCollectWindowInfo.mainForm.Width + SamMath.RandomRange(15f, 20f);
                                break;
                            case TheGoose.Task_CollectWindow.ScreenDirection.Top:
                                TheGoose.targetPos.y = (float)TheGoose.taskCollectWindowInfo.mainForm.Height + SamMath.RandomRange(80f, 100f);
                                TheGoose.targetPos.x = SamMath.Lerp(TheGoose.position.x, (float)(Program.mainForm.Width / 2), SamMath.RandomRange(0.2f, 0.3f));
                                break;
                            case TheGoose.Task_CollectWindow.ScreenDirection.Right:
                                TheGoose.targetPos.y = SamMath.Lerp(TheGoose.position.y, (float)(Program.mainForm.Height / 2), SamMath.RandomRange(0.2f, 0.3f));
                                TheGoose.targetPos.x = (float)Program.mainForm.Width - ((float)TheGoose.taskCollectWindowInfo.mainForm.Width + SamMath.RandomRange(20f, 30f));
                                break;
                        }
                        TheGoose.targetPos.x = SamMath.Clamp(TheGoose.targetPos.x, (float)(TheGoose.taskCollectWindowInfo.mainForm.Width + 55), (float)(Program.mainForm.Width - (TheGoose.taskCollectWindowInfo.mainForm.Width + 55)));
                        TheGoose.targetPos.y = SamMath.Clamp(TheGoose.targetPos.y, (float)(TheGoose.taskCollectWindowInfo.mainForm.Height + 80), (float)Program.mainForm.Height);
                        TheGoose.taskCollectWindowInfo.stage = TheGoose.Task_CollectWindow.Stage.DraggingWindowBack;
                        return;
                    }
                    break;
                case TheGoose.Task_CollectWindow.Stage.DraggingWindowBack:
                    if (Vector2.Distance(TheGoose.position, TheGoose.targetPos) < 5f)
                    {
                        TheGoose.targetPos = TheGoose.position + Vector2.GetFromAngleDegrees(TheGoose.direction + 180f) * 40f;
                        TheGoose.SetTask(TheGoose.GooseTask.Wander);
                        return;
                    }
                    TheGoose.overrideExtendNeck = true;
                    TheGoose.targetDirection = TheGoose.position - TheGoose.targetPos;
                    TheGoose.taskCollectWindowInfo.mainForm.SetWindowPositionThreadsafe(TheGoose.ToIntPoint(TheGoose.gooseRig.head2EndPoint - TheGoose.taskCollectWindowInfo.windowOffsetToBeak));
                    break;
                default:
                    return;
            }
        }

        private static void CollectMemeTask_CancelEarly(object sender, FormClosingEventArgs e)
        {
            Logger.LogMethod();
            TheGoose.SetTask(TheGoose.GooseTask.NabMouse);
        }

        private static void RunTrackMud()
        {
            Logger.LogMethod();
            switch (TheGoose.taskTrackMudInfo.stage)
            {
                case TheGoose.Task_TrackMud.Stage.DecideToRun:
                    TheGoose.SetTargetOffscreen(false);
                    TheGoose.SetSpeed(TheGoose.SpeedTiers.Run);
                    TheGoose.taskTrackMudInfo.stage = TheGoose.Task_TrackMud.Stage.RunningOffscreen;
                    return;
                case TheGoose.Task_TrackMud.Stage.RunningOffscreen:
                    if (Vector2.Distance(TheGoose.position, TheGoose.targetPos) < 5f)
                    {
                        TheGoose.targetPos = new Vector2(SamMath.RandomRange(0f, (float)Program.mainForm.Width), SamMath.RandomRange(0f, (float)Program.mainForm.Height));
                        TheGoose.taskTrackMudInfo.nextDirChangeTime = Time.time + TheGoose.Task_TrackMud.GetDirChangeInterval();
                        TheGoose.taskTrackMudInfo.timeToStopRunning = Time.time + 2f;
                        TheGoose.trackMudEndTime = Time.time + 15f;
                        TheGoose.taskTrackMudInfo.stage = TheGoose.Task_TrackMud.Stage.RunningWandering;
                        Sound.PlayMudSquith();
                        return;
                    }
                    break;
                case TheGoose.Task_TrackMud.Stage.RunningWandering:
                    if (Vector2.Distance(TheGoose.position, TheGoose.targetPos) < 5f || Time.time > TheGoose.taskTrackMudInfo.nextDirChangeTime)
                    {
                        TheGoose.targetPos = new Vector2(SamMath.RandomRange(0f, (float)Program.mainForm.Width), SamMath.RandomRange(0f, (float)Program.mainForm.Height));
                        TheGoose.taskTrackMudInfo.nextDirChangeTime = Time.time + TheGoose.Task_TrackMud.GetDirChangeInterval();
                    }
                    if (Time.time > TheGoose.taskTrackMudInfo.timeToStopRunning)
                    {
                        TheGoose.targetPos = TheGoose.position + new Vector2(30f, 3f);
                        TheGoose.targetPos.x = SamMath.Clamp(TheGoose.targetPos.x, 55f, (float)(Program.mainForm.Width - 55));
                        TheGoose.targetPos.y = SamMath.Clamp(TheGoose.targetPos.y, 80f, (float)(Program.mainForm.Height - 80));
                        TheGoose.SetTask(TheGoose.GooseTask.Wander, false);
                    }
                    break;
                default:
                    return;
            }
        }

        private static void ChooseNextTask()
        {
            Logger.LogMethod();
            if (!GooseConfig.settings.CanAttackAtRandom && Time.time < GooseConfig.settings.FirstWanderTimeSeconds + 1f)
            {
                TheGoose.SetTask(TheGoose.GooseTask.TrackMud);
                return;
            }
            if (Time.time > 480f && !TheGoose.hasAskedForDonation)
            {
                TheGoose.hasAskedForDonation = true;
                TheGoose.SetTask(TheGoose.GooseTask.CollectWindow_Donate);
                return;
            }
            TheGoose.GooseTask gooseTask = TheGoose.gooseTaskWeightedList[TheGoose.taskPickerDeck.Next()];
            while (!GooseConfig.settings.CanAttackAtRandom)
            {
                if (gooseTask != TheGoose.GooseTask.NabMouse)
                {
                    break;
                }
                gooseTask = TheGoose.gooseTaskWeightedList[TheGoose.taskPickerDeck.Next()];
            }
            TheGoose.SetTask(gooseTask);
        }

        private static void SetTask(TheGoose.GooseTask task)
        {
            Logger.LogMethod();
            TheGoose.SetTask(task, true);
        }

        private static void SetTask(TheGoose.GooseTask task, bool honck)
        {
            Logger.LogMethod();
            if (honck)
            {
                Sound.HONCC();
            }
            TheGoose.currentTask = task;
            switch (task)
            {
                case TheGoose.GooseTask.Wander:
                    TheGoose.SetSpeed(TheGoose.SpeedTiers.Walk);
                    TheGoose.taskWanderInfo = default(TheGoose.Task_Wander);
                    TheGoose.taskWanderInfo.pauseStartTime = -1f;
                    TheGoose.taskWanderInfo.wanderingStartTime = Time.time;
                    TheGoose.taskWanderInfo.wanderingDuration = TheGoose.Task_Wander.GetRandomWanderDuration();
                    return;
                case TheGoose.GooseTask.NabMouse:
                    TheGoose.taskNabMouseInfo = default(TheGoose.Task_NabMouse);
                    TheGoose.taskNabMouseInfo.chaseStartTime = Time.time;
                    return;
                case TheGoose.GooseTask.CollectWindow_Meme:
                    TheGoose.taskCollectWindowInfo = default(TheGoose.Task_CollectWindow);
                    TheGoose.taskCollectWindowInfo.mainForm = new TheGoose.SimpleImageForm();
                    TheGoose.SetTask(TheGoose.GooseTask.CollectWindow_DONOTSET, false);
                    return;
                case TheGoose.GooseTask.CollectWindow_Notepad:
                    TheGoose.taskCollectWindowInfo = default(TheGoose.Task_CollectWindow);
                    TheGoose.taskCollectWindowInfo.mainForm = new TheGoose.SimpleTextForm();
                    TheGoose.SetTask(TheGoose.GooseTask.CollectWindow_DONOTSET, false);
                    return;
                case TheGoose.GooseTask.CollectWindow_Donate:
                    TheGoose.taskCollectWindowInfo = default(TheGoose.Task_CollectWindow);
                    TheGoose.taskCollectWindowInfo.mainForm = new TheGoose.SimpleDonateForm();
                    TheGoose.SetTask(TheGoose.GooseTask.CollectWindow_DONOTSET, false);
                    return;
                case TheGoose.GooseTask.CollectWindow_DONOTSET:
                    TheGoose.taskCollectWindowInfo.screenDirection = TheGoose.SetTargetOffscreen(false);
                    switch (TheGoose.taskCollectWindowInfo.screenDirection)
                    {
                        case TheGoose.Task_CollectWindow.ScreenDirection.Left:
                            TheGoose.taskCollectWindowInfo.windowOffsetToBeak = new Vector2((float)TheGoose.taskCollectWindowInfo.mainForm.Width, (float)(TheGoose.taskCollectWindowInfo.mainForm.Height / 2));
                            return;
                        case TheGoose.Task_CollectWindow.ScreenDirection.Top:
                            TheGoose.taskCollectWindowInfo.windowOffsetToBeak = new Vector2((float)(TheGoose.taskCollectWindowInfo.mainForm.Width / 2), (float)TheGoose.taskCollectWindowInfo.mainForm.Height);
                            return;
                        case TheGoose.Task_CollectWindow.ScreenDirection.Right:
                            TheGoose.taskCollectWindowInfo.windowOffsetToBeak = new Vector2(0f, (float)(TheGoose.taskCollectWindowInfo.mainForm.Height / 2));
                            return;
                        default:
                            return;
                    }
                    break;
                case TheGoose.GooseTask.TrackMud:
                    TheGoose.taskTrackMudInfo = default(TheGoose.Task_TrackMud);
                    return;
                default:
                    return;
            }
        }

        private static void RunAI()
        {
            Logger.LogMethod();
            switch (TheGoose.currentTask)
            {
                case TheGoose.GooseTask.Wander:
                    TheGoose.RunWander();
                    return;
                case TheGoose.GooseTask.NabMouse:
                    TheGoose.RunNabMouse();
                    return;
                case TheGoose.GooseTask.CollectWindow_Meme:
                case TheGoose.GooseTask.CollectWindow_Notepad:
                case TheGoose.GooseTask.CollectWindow_Donate:
                    break;
                case TheGoose.GooseTask.CollectWindow_DONOTSET:
                    TheGoose.RunCollectWindow();
                    return;
                case TheGoose.GooseTask.TrackMud:
                    TheGoose.RunTrackMud();
                    break;
                default:
                    return;
            }
        }

        private static TheGoose.Task_CollectWindow.ScreenDirection SetTargetOffscreen(bool canExitTop = false)
        {
            Logger.LogMethod();
            int num = (int)TheGoose.position.x;
            TheGoose.Task_CollectWindow.ScreenDirection result = TheGoose.Task_CollectWindow.ScreenDirection.Left;
            TheGoose.targetPos = new Vector2(-50f, SamMath.Lerp(TheGoose.position.y, (float)(Program.mainForm.Height / 2), 0.4f));
            if (num > Program.mainForm.Width / 2)
            {
                num = Program.mainForm.Width - (int)TheGoose.position.x;
                result = TheGoose.Task_CollectWindow.ScreenDirection.Right;
                TheGoose.targetPos = new Vector2((float)(Program.mainForm.Width + 50), SamMath.Lerp(TheGoose.position.y, (float)(Program.mainForm.Height / 2), 0.4f));
            }
            if (canExitTop && (float)num > TheGoose.position.y)
            {
                result = TheGoose.Task_CollectWindow.ScreenDirection.Top;
                TheGoose.targetPos = new Vector2(SamMath.Lerp(TheGoose.position.x, (float)(Program.mainForm.Width / 2), 0.4f), -50f);
            }
            return result;
        }

        private static void SolveFeet()
        {
            Logger.LogMethod();
            Vector2.GetFromAngleDegrees(TheGoose.direction);
            Vector2.GetFromAngleDegrees(TheGoose.direction + 90f);
            Vector2 footHome = TheGoose.GetFootHome(false);
            Vector2 footHome2 = TheGoose.GetFootHome(true);
            if (TheGoose.lFootMoveTimeStart < 0f && TheGoose.rFootMoveTimeStart < 0f)
            {
                if (Vector2.Distance(TheGoose.lFootPos, footHome) > 5f)
                {
                    TheGoose.lFootMoveOrigin = TheGoose.lFootPos;
                    TheGoose.lFootMoveDir = Vector2.Normalize(footHome - TheGoose.lFootPos);
                    TheGoose.lFootMoveTimeStart = Time.time;
                    return;
                }
                if (Vector2.Distance(TheGoose.rFootPos, footHome2) > 5f)
                {
                    TheGoose.rFootMoveOrigin = TheGoose.rFootPos;
                    TheGoose.rFootMoveDir = Vector2.Normalize(footHome2 - TheGoose.rFootPos);
                    TheGoose.rFootMoveTimeStart = Time.time;
                    return;
                }
            }
            else if (TheGoose.lFootMoveTimeStart > 0f)
            {
                Vector2 b = footHome + TheGoose.lFootMoveDir * 0.4f * 5f;
                if (Time.time <= TheGoose.lFootMoveTimeStart + TheGoose.stepTime)
                {
                    float p = (Time.time - TheGoose.lFootMoveTimeStart) / TheGoose.stepTime;
                    TheGoose.lFootPos = Vector2.Lerp(TheGoose.lFootMoveOrigin, b, Easings.CubicEaseInOut(p));
                    return;
                }
                TheGoose.lFootPos = b;
                TheGoose.lFootMoveTimeStart = -1f;
                Sound.PlayPat();
                if (Time.time < TheGoose.trackMudEndTime)
                {
                    TheGoose.AddFootMark(TheGoose.lFootPos);
                    return;
                }
            }
            else if (TheGoose.rFootMoveTimeStart > 0f)
            {
                Vector2 b2 = footHome2 + TheGoose.rFootMoveDir * 0.4f * 5f;
                if (Time.time > TheGoose.rFootMoveTimeStart + TheGoose.stepTime)
                {
                    TheGoose.rFootPos = b2;
                    TheGoose.rFootMoveTimeStart = -1f;
                    Sound.PlayPat();
                    if (Time.time < TheGoose.trackMudEndTime)
                    {
                        TheGoose.AddFootMark(TheGoose.rFootPos);
                        return;
                    }
                }
                else
                {
                    float p2 = (Time.time - TheGoose.rFootMoveTimeStart) / TheGoose.stepTime;
                    TheGoose.rFootPos = Vector2.Lerp(TheGoose.rFootMoveOrigin, b2, Easings.CubicEaseInOut(p2));
                }
            }
        }

        private static Vector2 GetFootHome(bool rightFoot)
        {
            Logger.LogMethod();
            float b = (float)(rightFoot ? 1 : 0);
            Vector2 a = Vector2.GetFromAngleDegrees(TheGoose.direction + 90f) * b;
            return TheGoose.position + a * 6f;
        }

        private static void AddFootMark(Vector2 markPos)
        {
            Logger.LogMethod();
            TheGoose.footMarks[TheGoose.footMarkIndex].time = Time.time;
            TheGoose.footMarks[TheGoose.footMarkIndex].position = markPos;
            TheGoose.footMarkIndex++;
            if (TheGoose.footMarkIndex >= TheGoose.footMarks.Length)
            {
                TheGoose.footMarkIndex = 0;
            }
        }

        public static void UpdateRig()
        {
            Logger.LogMethod();
            float num = TheGoose.direction;
            int num2 = (int)TheGoose.position.x;
            int num3 = (int)TheGoose.position.y;
            Vector2 a = new Vector2((float)num2, (float)num3);
            Vector2 b = new Vector2(1.3f, 0.4f);
            Vector2 fromAngleDegrees = Vector2.GetFromAngleDegrees(num);
            Vector2 perpendicularVector = Vector2.GetFromAngleDegrees(num + 90f) * b;
            Vector2 a2 = new Vector2(0f, -1f);
            TheGoose.gooseRig.underbodyCenter = a + a2 * 9f;
            TheGoose.gooseRig.bodyCenter = a + a2 * 14f;
            int num4 = (int)SamMath.Lerp(20f, 10f, TheGoose.gooseRig.neckLerpPercent);
            int num5 = (int)SamMath.Lerp(3f, 16f, TheGoose.gooseRig.neckLerpPercent);
            TheGoose.gooseRig.neckCenter = a + a2 * (float)(14 + num4);
            TheGoose.gooseRig.neckBase = TheGoose.gooseRig.bodyCenter + fromAngleDegrees * 15f;
            TheGoose.gooseRig.neckHeadPoint = TheGoose.gooseRig.neckBase + fromAngleDegrees * (float)num5 + a2 * (float)num4;
            TheGoose.gooseRig.head1EndPoint = TheGoose.gooseRig.neckHeadPoint + fromAngleDegrees * 3f - a2 * 1f;
            TheGoose.gooseRig.head2EndPoint = TheGoose.gooseRig.head1EndPoint + fromAngleDegrees * 5f;
        }

        public static void Render(Graphics g)
        {
            Logger.LogMethod();
            for (int i = 0; i < TheGoose.footMarks.Length; i++)
            {
                if (TheGoose.footMarks[i].time != 0f)
                {
                    float num = TheGoose.footMarks[i].time + 8.5f;
                    float p = SamMath.Clamp(Time.time - num, 0f, 1f) / 1f;
                    float num2 = SamMath.Lerp(3f, 0f, p);
                    TheGoose.FillCircleFromCenter(g, Brushes.SaddleBrown, TheGoose.footMarks[i].position, (int)num2);
                }
            }
            TheGoose.UpdateRig();
            float num3 = TheGoose.direction;
            int num4 = (int)TheGoose.position.x;
            int num5 = (int)TheGoose.position.y;
            Vector2 vector = new Vector2((float)num4, (float)num5);
            Vector2 b = new Vector2(1.3f, 0.4f);
            Vector2 fromAngleDegrees = Vector2.GetFromAngleDegrees(num3);
            Vector2 a = new Vector2(0f, -1f);
            Vector2 fromAngleDegrees2 = new Vector2(-fromAngleDegrees.y, fromAngleDegrees.x);
            TheGoose.DrawingPen.Brush = Brushes.White;
            TheGoose.FillCircleFromCenter(g, Brushes.Orange, TheGoose.lFootPos, 4);
            TheGoose.FillCircleFromCenter(g, Brushes.Orange, TheGoose.rFootPos, 4);
            TheGoose.FillEllipseFromCenter(g, TheGoose.shadowBrush, (int)vector.x, (int)vector.y, 20, 15);
            TheGoose.DrawingPen.Color = Color.LightGray;
            TheGoose.DrawingPen.Width = 24f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.bodyCenter + fromAngleDegrees * 11f), TheGoose.ToIntPoint(TheGoose.gooseRig.bodyCenter - fromAngleDegrees * 11f));
            TheGoose.DrawingPen.Width = 15f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.neckBase), TheGoose.ToIntPoint(TheGoose.gooseRig.neckHeadPoint));
            TheGoose.DrawingPen.Width = 17f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.neckHeadPoint), TheGoose.ToIntPoint(TheGoose.gooseRig.head1EndPoint));
            TheGoose.DrawingPen.Width = 12f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.head1EndPoint), TheGoose.ToIntPoint(TheGoose.gooseRig.head2EndPoint));
            TheGoose.DrawingPen.Color = Color.LightGray;
            TheGoose.DrawingPen.Width = 15f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.underbodyCenter + fromAngleDegrees * 7f), TheGoose.ToIntPoint(TheGoose.gooseRig.underbodyCenter - fromAngleDegrees * 7f));
            TheGoose.DrawingPen.Color = Color.White; //cuerpo del ganso
            TheGoose.DrawingPen.Width = 22f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.bodyCenter + fromAngleDegrees * 11f), TheGoose.ToIntPoint(TheGoose.gooseRig.bodyCenter - fromAngleDegrees * 11f));
            TheGoose.DrawingPen.Width = 13f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.neckBase), TheGoose.ToIntPoint(TheGoose.gooseRig.neckHeadPoint));
            TheGoose.DrawingPen.Width = 15f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.neckHeadPoint), TheGoose.ToIntPoint(TheGoose.gooseRig.head1EndPoint));
            TheGoose.DrawingPen.Width = 10f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.head1EndPoint), TheGoose.ToIntPoint(TheGoose.gooseRig.head2EndPoint));
            TheGoose.DrawingPen.Width = 9f;
            TheGoose.DrawingPen.Brush = Brushes.Orange;
            Vector2 vector2 = TheGoose.gooseRig.head2EndPoint + fromAngleDegrees * 3f;
            g.DrawLine(TheGoose.DrawingPen, TheGoose.ToIntPoint(TheGoose.gooseRig.head2EndPoint), TheGoose.ToIntPoint(vector2));
            Vector2 pos = TheGoose.gooseRig.neckHeadPoint + a * 3f + -fromAngleDegrees2 * b * 5f + fromAngleDegrees * 5f;
            Vector2 pos2 = TheGoose.gooseRig.neckHeadPoint + a * 3f + fromAngleDegrees2 * b * 5f + fromAngleDegrees * 5f;
            TheGoose.FillCircleFromCenter(g, Brushes.Black, pos, 2);
            TheGoose.FillCircleFromCenter(g, Brushes.Black, pos2, 2);
        }

        public static void FillCircleFromCenter(Graphics g, Brush brush, Vector2 pos, int radius)
        {
            TheGoose.FillEllipseFromCenter(g, brush, (int)pos.x, (int)pos.y, radius, radius);
        }

        public static void FillCircleFromCenter(Graphics g, Brush brush, int x, int y, int radius)
        {
            TheGoose.FillEllipseFromCenter(g, brush, x, y, radius, radius);
        }

        public static void FillEllipseFromCenter(Graphics g, Brush brush, int x, int y, int xRadius, int yRadius)
        {
            g.FillEllipse(brush, x - xRadius, y - yRadius, xRadius * 2, yRadius * 2);
        }

        public static void FillEllipseFromCenter(Graphics g, Brush brush, Vector2 position, Vector2 xyRadius)
        {
            g.FillEllipse(brush, position.x - xyRadius.x, position.y - xyRadius.y, xyRadius.x * 2f, xyRadius.y * 2f);
        }

        private static Point ToIntPoint(Vector2 vector)
        {
            return new Point((int)vector.x, (int)vector.y);
        }

        private static Vector2 position;
        private static Vector2 velocity;
        private static float direction;
        private static Vector2 targetDirection;
        private static bool overrideExtendNeck;
        private const TheGoose.GooseTask FirstUX_FirstTask = TheGoose.GooseTask.TrackMud;
        private const TheGoose.GooseTask FirstUX_SecondTask = TheGoose.GooseTask.CollectWindow_Meme;
        private static Vector2 targetPos = new Vector2(300f, 300f);
        private static float targetDir = 90f;
        private static float currentSpeed = 80f;
        private static float currentAcceleration = 1300f;
        private static float stepTime = 0.2f;
        private const float WalkSpeed = 80f;
        private const float RunSpeed = 200f;
        private const float ChargeSpeed = 400f;
        private const float turnSpeed = 120f;
        private const float AccelerationNormal = 1300f;
        private const float AccelerationCharged = 2300f;
        private const float StopRadius = -10f;
        private const float StepTimeNormal = 0.2f;
        private const float StepTimeCharged = 0.1f;
        private static float trackMudEndTime = -1f;
        private const float DurationToTrackMud = 15f;
        private static Pen DrawingPen;
        private static Bitmap shadowBitmap;
        private static TextureBrush shadowBrush;
        private static Pen shadowPen;
        private static FootMark[] footMarks = new FootMark[64];
        private static int footMarkIndex = 0;
        private static bool lastFrameMouseButtonPressed = false;
        private static TheGoose.GooseTask currentTask;
        private static TheGoose.Task_Wander taskWanderInfo;
        private static TheGoose.Task_NabMouse taskNabMouseInfo;
        private static Rectangle tmpRect = default(Rectangle);
        private static Size tmpSize = default(Size);
        private static bool hasAskedForDonation = false;
        private static TheGoose.Task_CollectWindow taskCollectWindowInfo;
        private static TheGoose.Task_TrackMud taskTrackMudInfo;
        private static TheGoose.GooseTask[] gooseTaskWeightedList = new TheGoose.GooseTask[]
        {
            TheGoose.GooseTask.TrackMud,
            TheGoose.GooseTask.TrackMud,
            TheGoose.GooseTask.CollectWindow_Meme,
            TheGoose.GooseTask.CollectWindow_Meme,
            TheGoose.GooseTask.CollectWindow_Notepad,
            TheGoose.GooseTask.NabMouse,
            TheGoose.GooseTask.NabMouse,
            TheGoose.GooseTask.NabMouse
        };
        private static Deck taskPickerDeck = new Deck(TheGoose.gooseTaskWeightedList.Length);
        private static Vector2 lFootPos;
        private static Vector2 rFootPos;
        private static float lFootMoveTimeStart = -1f;
        private static float rFootMoveTimeStart = -1f;
        private static Vector2 lFootMoveOrigin;
        private static Vector2 rFootMoveOrigin;
        private static Vector2 lFootMoveDir;
        private static Vector2 rFootMoveDir;
        private const float wantStepAtDistance = 5f;
        private const int feetDistanceApart = 6;
        private const float overshootFraction = 0.4f;
        private static TheGoose.Rig gooseRig;

        private enum SpeedTiers
        {
            Walk,
            Run,
            Charge
        }

        private enum GooseTask
        {
            Wander,
            NabMouse,
            CollectWindow_Meme,
            CollectWindow_Notepad,
            CollectWindow_Donate,
            CollectWindow_DONOTSET,
            TrackMud,
            Count
        }

        private struct Task_Wander
        {
            public static float GetRandomPauseDuration()
            {
                return 1f + (float)SamMath.Rand.NextDouble() * 1f;
            }
            public static float GetRandomWanderDuration()
            {
                if (Time.time < 1f)
                {
                    return GooseConfig.settings.FirstWanderTimeSeconds;
                }
                return SamMath.RandomRange(GooseConfig.settings.MinWanderingTimeSeconds, GooseConfig.settings.MaxWanderingTimeSeconds);
            }
            public static float GetRandomWalkTime()
            {
                return SamMath.RandomRange(1f, 6f);
            }
            private const float MinPauseTime = 1f;
            private const float MaxPauseTime = 2f;
            public const float GoodEnoughDistance = 20f;
            public float wanderingStartTime;
            public float wanderingDuration;
            public float pauseStartTime;
            public float pauseDuration;
        }

        private struct Task_NabMouse
        {
            public TheGoose.Task_NabMouse.Stage currentStage;
            public Vector2 dragToPoint;
            public float grabbedOriginalTime;
            public float chaseStartTime;
            public Vector2 originalVectorToMouse;
            public const float MouseGrabDistance = 15f;
            public const float MouseSuccTime = 0.06f;
            public const float MouseDropDistance = 30f;
            public const float MinRunTime = 2f;
            public const float MaxRunTime = 4f;
            public const float GiveUpTime = 9f;
            public static readonly Vector2 StruggleRange = new Vector2(3f, 3f);
            public enum Stage
            {
                SeekingMouse,
                DraggingMouseAway,
                Decelerating
            }
        }

        private struct Task_CollectWindow
        {
            public static float GetWaitTime()
            {
                return SamMath.RandomRange(2f, 3.5f);
            }
            public TheGoose.MovableForm mainForm;
            public TheGoose.Task_CollectWindow.Stage stage;
            public float secsToWait;
            public float waitStartTime;
            public TheGoose.Task_CollectWindow.ScreenDirection screenDirection;
            public Vector2 windowOffsetToBeak;
            public enum Stage
            {
                WalkingOffscreen,
                WaitingToBringWindowBack,
                DraggingWindowBack
            }
            public enum ScreenDirection
            {
                Left,
                Top,
                Right
            }
        }

        private class MovableForm : Form
        {
            public MovableForm()
            {
                base.StartPosition = FormStartPosition.Manual;
                base.Width = 400;
                base.Height = 400;
                this.BackColor = Color.DimGray;
                base.Icon = null;
                base.ShowIcon = false;
                this.SetWindowResizableThreadsafe(false);
            }
            public void SetWindowPositionThreadsafe(Point p)
            {
                if (base.InvokeRequired)
                {
                    base.BeginInvoke(new MethodInvoker(delegate ()
                    {
                        this.Location = p;
                        this.TopMost = true;
                    }));
                    return;
                }
                base.Location = p;
                base.TopMost = true;
            }
            public void SetWindowResizableThreadsafe(bool canResize)
            {
                if (base.InvokeRequired)
                {
                    base.BeginInvoke(new MethodInvoker(delegate ()
                    {
                        this.FormBorderStyle = (canResize ? FormBorderStyle.Sizable : FormBorderStyle.FixedSingle);
                        this.MaximizeBox = (this.MinimizeBox = canResize);
                    }));
                    return;
                }
                base.FormBorderStyle = (canResize ? FormBorderStyle.Sizable : FormBorderStyle.FixedSingle);
                base.MaximizeBox = (base.MinimizeBox = canResize);
            }
        }

        private class SimpleImageForm : TheGoose.MovableForm
        {
            public SimpleImageForm()
            {
                List<Image> list = new List<Image>();
                try
                {
                    string[] files = Directory.GetFiles(TheGoose.SimpleImageForm.memesRootFolder);
                    for (int i = 0; i < files.Length; i++)
                    {
                        Image image = Image.FromFile(files[i]);
                        if (image != null)
                        {
                            list.Add(image);
                        }
                    }
                }
                catch
                {
                }
                this.localImages = list.ToArray();
                this.localImageDeck = new Deck(this.localImages.Length);
                PictureBox pictureBox = new PictureBox();
                pictureBox.Dock = DockStyle.Fill;
                try
                {
                    pictureBox.Image = this.localImages[this.localImageDeck.Next()];
                }
                catch
                {
                    pictureBox.LoadAsync(TheGoose.SimpleImageForm.imageURLs[TheGoose.SimpleImageForm.imageURLDeck.Next()]);
                }
                pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                base.Controls.Add(pictureBox);
            }
            private static readonly string memesRootFolder = Program.GetPathToFileInAssembly("Assets/Images/Memes/");
            private Image[] localImages;
            private Deck localImageDeck;
            private static string[] imageURLs = new string[]
            {
                "https://preview.redd.it/dsfjv8aev0p31.png?width=960&crop=smart&auto=webp&s=1d58948acc5c6dd60df1092c1bd2a59a509069fd",
                "https://i.redd.it/4ojv59zvglp31.jpg",
                "https://i.redd.it/4bamd6lnso241.jpg",
                "https://i.redd.it/5i5et9p1vsp31.jpg",
                "https://i.redd.it/j2f1i9djx5p31.jpg"
            };
            private static Deck imageURLDeck = new Deck(TheGoose.SimpleImageForm.imageURLs.Length);
        }

        private class SimpleTextForm : TheGoose.MovableForm
        {
            public SimpleTextForm()
            {
                base.Width = 200;
                base.Height = 150;
                this.Text = "Goose \"Not-epad\"";
                TextBox textBox = new TextBox();
                textBox.Multiline = true;
                textBox.AcceptsReturn = true;
                textBox.Text = TheGoose.SimpleTextForm.possiblePhrases[TheGoose.SimpleTextForm.textIndices.Next()];
                textBox.Location = new Point(0, 0);
                textBox.Width = base.ClientSize.Width;
                textBox.Height = base.ClientSize.Height - 5;
                textBox.Select(textBox.Text.Length, 0);
                textBox.Font = new Font(textBox.Font.FontFamily, 10f, FontStyle.Regular);
                base.Controls.Add(textBox);
                string text = Environment.SystemDirectory + "\\notepad.exe";
                if (File.Exists(text))
                {
                    try
                    {
                        base.Icon = Icon.ExtractAssociatedIcon(text);
                        base.ShowIcon = true;
                    }
                    catch
                    {
                    }
                }
            }
            private void ExitWindow(object sender, EventArgs e)
            {
                base.Close();
            }
            private static string[] possiblePhrases = new string[]
            {
                "am goose hjonk",
                "good work",
                "nsfdafdsaafsdjl\r\nasdas       sorry\r\nhard to type withh feet",
                "i cause problems on purpose",
                "\"peace was never an option\"\r\n   -the goose (me)",
                "\r\n\r\n  >o) \r\n    (_>"
            };
            private static Deck textIndices = new Deck(TheGoose.SimpleTextForm.possiblePhrases.Length);
        }

        private class SimpleDonateForm : TheGoose.MovableForm
        {
            public SimpleDonateForm()
            {
                new PictureBox();
                base.ClientSize = new Size((int)(250f * this.scale), (int)(300f * this.scale));
                try
                {
                    this.BackgroundImage = Image.FromFile(TheGoose.SimpleDonateForm.donationGraphicSrc);
                }
                catch
                {
                    Label label = new Label();
                    label.Text = "Can't find the donation image... are you messing with the game files?\nCheck out my Twitter at twitter.com/samnchiet I guess?";
                    label.Location = new Point(0, 0);
                    label.Width = base.ClientSize.Width;
                    label.Height = base.ClientSize.Height;
                    label.BackColor = Color.White;
                    label.TextAlign = ContentAlignment.MiddleCenter;
                    base.Controls.Add(label);
                }
                this.BackgroundImageLayout = ImageLayout.Stretch;
                base.Controls.Add(this.SetupButton(111, 407, 390, 475, new EventHandler(this.OpenPatreonLink), true));
                base.Controls.Add(this.SetupButton(174, 500, 325, 545, new EventHandler(this.OpenPaypalLink), true));
                base.Controls.Add(this.SetupButton(381, 302, 433, 360, new EventHandler(this.OpenDiscordLink), true));
                base.Controls.Add(this.SetupButton(403, 247, 472, 312, new EventHandler(this.OpenTwitterLink), true));
            }
            private Button SetupButton(int point1X, int point1Y, int point2X, int point2Y, EventHandler handler, bool showHoverClick = true)
            {
                Button button = new Button();
                button.Location = new Point((int)((float)point1X * this.scale) / 2, (int)((float)point1Y * this.scale) / 2);
                button.Size = new Size((int)((float)(point2X - point1X) * this.scale) / 2, (int)((float)(point2Y - point1Y) * this.scale) / 2);
                button.Click += handler;
                button.Cursor = Cursors.Hand;
                button.BackColor = Color.Transparent;
                button.ForeColor = Color.Transparent;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.MouseOverBackColor = (showHoverClick ? Color.FromArgb(40, Color.White) : Color.Transparent);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, Color.White);
                button.FlatAppearance.BorderSize = 0;
                button.TabStop = false;
                return button;
            }
            private void OpenPatreonLink(object sender, EventArgs e)
            {
                Process.Start("https://www.patreon.com/bePatron?u=3541875");
            }
            private void OpenPaypalLink(object sender, EventArgs e)
            {
                Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=WUKYHY7SZ275Q&currency_code=USD&source=url");
            }
            private void OpenTwitterLink(object sender, EventArgs e)
            {
                Process.Start("https://www.twitter.com/samnchiet");
            }
            private void OpenDiscordLink(object sender, EventArgs e)
            {
                Process.Start("https://discord.gg/PCJS6DH");
            }
            private const string patreonLink = "https://www.patreon.com/bePatron?u=3541875";
            private const string paypalLink = "https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=WUKYHY7SZ275Q&currency_code=USD&source=url";
            private const string twitterLink = "https://www.twitter.com/samnchiet";
            private const string discordLink = "https://discord.gg/PCJS6DH";
            private static string donationGraphicSrc = Program.GetPathToFileInAssembly("Assets/Images/OtherGfx/DonatePage.png");
            private float scale = 1.25f;
        }

        private struct Task_TrackMud
        {
            public static float GetDirChangeInterval()
            {
                return 100f;
            }
            public const float DurationToRunAmok = 2f;
            public float nextDirChangeTime;
            public float timeToStopRunning;
            public TheGoose.Task_TrackMud.Stage stage;
            public enum Stage
            {
                DecideToRun,
                RunningOffscreen,
                RunningWandering
            }
        }

        private struct Rig
        {
            public const int UnderBodyRadius = 15;
            public const int UnderBodyLength = 7;
            public const int UnderBodyElevation = 9;
            public Vector2 underbodyCenter;
            public const int BodyRadius = 22;
            public const int BodyLength = 11;
            public const int BodyElevation = 14;
            public Vector2 bodyCenter;
            public const int NeccRadius = 13;
            public const int NeccHeight1 = 20;
            public const int NeccExtendForward1 = 3;
            public const int NeccHeight2 = 10;
            public const int NeccExtendForward2 = 16;
            public float neckLerpPercent;
            public Vector2 neckCenter;
            public Vector2 neckBase;
            public Vector2 neckHeadPoint;
            public const int HeadRadius1 = 15;
            public const int HeadLength1 = 3;
            public const int HeadRadius2 = 10;
            public const int HeadLength2 = 5;
            public Vector2 head1EndPoint;
            public Vector2 head2EndPoint;
            public const int EyeRadius = 2;
            public const int EyeElevation = 3;
            public const float IPD = 5f;
            public const float EyesForward = 5f;
        }
    }
}