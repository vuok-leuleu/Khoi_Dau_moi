using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuestObjective
{
    [TextArea(1, 2)]
    public string title = "Tên nhiệm vụ";
    public bool isCompleted = false;

    [Header("--- PHẦN THƯỞNG BẠN TỰ CẤU HÌNH ---")]
    public int rewardGold = 0;
    public int rewardWood = 0;
    public int rewardStone = 0;
    public int rewardWheat = 0;
}

[Serializable]
public class ChapterData
{
    public string chapterName = "Chapter One";

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