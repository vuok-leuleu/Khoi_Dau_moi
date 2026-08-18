using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuestObjective
{
    [Tooltip("ID ổn định để lưu tiến độ sau này. Để trống vẫn tương thích dữ liệu cũ.")]
    public string questId;

    [TextArea(1, 2)]
    public string title = "Tên nhiệm vụ";
    public bool isCompleted = false;

    [Min(1)] public int targetProgress = 1;
    [Min(0)] public int currentProgress = 0;

    [Header("--- PHẦN THƯỞNG BẠN TỰ CẤU HÌNH ---")]
    public int rewardGold = 0;
    public int rewardWood = 0;
    public int rewardStone = 0;
    public int rewardWheat = 0;
}

[Serializable]
public class ChapterData
{
    [Tooltip("ID ổn định để lưu tiến độ sau này. Để trống vẫn tương thích dữ liệu cũ.")]
    public string chapterId;
    public string chapterName = "Chapter One";

    [TextArea(1, 3)] public string chapterDescription;
    public Sprite chapterBanner;
    [HideInInspector] public bool isCompleted;
    [HideInInspector] public bool isRewardClaimed;

    [Header("--- THƯỞNG KHI HOÀN THÀNH CHƯƠNG ---")]
    public int rewardGold = 200;
    public int rewardWood = 200;
    public int rewardStone = 200;

    [Header("--- PHẦN THƯỞNG DANH HIỆU / ITEM ---")]
    public string keyRewardName = "Honorary Crownguard";
    public Sprite keyRewardIcon;

    [Header("--- DANH SÁCH MỤC TIÊU ---")]
    public List<QuestObjective> objectives = new List<QuestObjective>();
}
