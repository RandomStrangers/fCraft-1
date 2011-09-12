﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org>
using System;

namespace fCraft {
    public sealed class ChatTimer {
        public static readonly TimeSpan MinDuration = TimeSpan.FromSeconds( 1 );

        string Message { get; set; }

        DateTime StartTime { get; set; }

        DateTime EndTime { get; set; }

        TimeSpan Duration { get; set; }

        public TimeSpan TimeLeft {
            get {
                return EndTime.Subtract( DateTime.UtcNow );
            }
        }

        int announceIntervalIndex;


        ChatTimer( TimeSpan duration, string message ) {
            Message = message;
            StartTime = DateTime.UtcNow;
            EndTime = StartTime.Add( duration );
            Duration = duration;
            for( int i = 0; i < AnnounceIntervals.Length; i++ ) {
                if( duration <= AnnounceIntervals[i] ) {
                    announceIntervalIndex = i - 1;
                    return;
                }
            }
            announceIntervalIndex = AnnounceIntervals.Length - 1;
        }


        static void TimerCallback( SchedulerTask task ) {
            ChatTimer timer = (ChatTimer)task.UserState;
            if( task.MaxRepeats == 1 ) {
                if( String.IsNullOrEmpty( timer.Message ) ) {
                    Chat.SendSay( Player.Console, "(Timer Up)" );
                } else {
                    Chat.SendSay( Player.Console, "(Timer Up) " + timer.Message );
                }
            } else if( timer.announceIntervalIndex >= 0 && timer.TimeLeft <= AnnounceIntervals[timer.announceIntervalIndex] ) {
                string timeLeft = AnnounceIntervals[timer.announceIntervalIndex].ToMiniString();
                if( String.IsNullOrEmpty( timer.Message ) ) {
                    Chat.SendSay( Player.Console, "(Timer) " + timeLeft );
                } else {
                    Chat.SendSay( Player.Console,
                                  String.Format( "(Timer) {0} until {1}",
                                                 timeLeft,
                                                 timer.Message ) );
                }
                timer.announceIntervalIndex--;
            }
        }


        public static void Start( TimeSpan duration, string message ) {
            if( duration < MinDuration ) {
                throw new ArgumentException( "Timer duration should be at least 1s", "duration" );
            }
            ChatTimer timer = new ChatTimer( duration, message );
            int oneSecondRepeats = (int)duration.TotalSeconds + 1;
            Scheduler.NewTask( TimerCallback, timer ).RunRepeating( TimeSpan.Zero,
                                                                    TimeSpan.FromSeconds( 1 ),
                                                                    oneSecondRepeats );
        }


        static readonly TimeSpan[] AnnounceIntervals = new[] {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(40),
            TimeSpan.FromSeconds(50),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(4),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(20),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(40),
            TimeSpan.FromMinutes(50),
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(3),
            TimeSpan.FromHours(4),
            TimeSpan.FromHours(5),
            TimeSpan.FromHours(10),
            TimeSpan.FromHours(20),
            TimeSpan.FromHours(30),
            TimeSpan.FromHours(40),
            TimeSpan.FromHours(50)
        };
    }
}