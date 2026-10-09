using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LevelData
{
    public GridSize gridSize;
    public int totalBlock;
    public List<BlockData> blocks;
    public List<BlindQueueColumn> blindQueues;
}

[System.Serializable]
public class GridSize
{
    public int x;
    public int y;
}

[System.Serializable]
public class BlockData
{
    public Position position;
    public int type;
    public int linkType;
    public BlockState state;
    public Position targetDirection;
    public List<AttachmentData> attachments;
}
//电子邮件puhalskijsemen@gmail.com
//源码网站 开vpn全局模式打开 https://web3incubators.com/
//电报https://t.me/gamecode999
//网页客服 https://web3incubators.com/kefu.html
[System.Serializable]
public class Position
{
    public int x;
    public int y;
}

[System.Serializable]
public class BlockState
{
    public bool isHidden;
    public bool isFrozen;
    public bool isLocked;
    public bool isKey;
    public int groupID;
    public int color;
}

[System.Serializable]
public class BlindQueueColumn
{
    public List<QueueItem> column;
}

[System.Serializable]
public class QueueItem
{
    public int groupID;
    public int color;
    public bool isHidden;
}

[System.Serializable]
public class AttachmentData
{
    public bool isHidden;
    public bool isFrozen;
    public bool isKey;
    public bool isLocked;
    public int groupID;
    public int color;
}
