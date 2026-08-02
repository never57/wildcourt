using System;

namespace WildCourt.Net.Protocol
{
    // ---------- Activity（文档 02 §2.2 ActivityController、13） ----------

    [Serializable]
    public class ActivityItem
    {
        public string id;
        public string name;
        public string startTime;  // "yyyy-MM-dd HH:mm:ss"（服务器时区 UTC+8）
        public string endTime;
        public string type;       // festival / limited / task ...
    }

    [Serializable]
    public class ActivityListResponse
    {
        public ActivityItem[] activities;
    }

    [Serializable]
    public class ActivityClaimRequest
    {
        public string activityId;
    }

    // ---------- DailySignIn（DailySignInController、文档 11） ----------

    [Serializable]
    public class SignInTodayResponse
    {
        public bool signedToday;
        public int days;          // 连续签到天数
    }

    // ---------- MiniGame（MiniGameController、文档 10） ----------

    [Serializable]
    public class MiniGameStartRequest
    {
        public string gameId;     // puzzle / spot / memory / catch / rhythm
    }

    [Serializable]
    public class MiniGameFinishRequest
    {
        public string gameId;
        public int score;         // 服务端二次验分，文档 10 §10.8
    }

    [Serializable]
    public class MiniGameFinishResponse
    {
        public string grade;      // S / A / B / C
        public int reward;        // 庭贝 50/35/20/10
    }

    [Serializable]
    public class MiniGameCapResponse
    {
        public int dailyCap;      // 200，文档 10 §10.5
        public int todayEarned;
    }
}
