namespace PackVisionApp.Jidam
{

/// <summary>JidamVision4.Core.Define / ImageSpace 와 동일한 검사·채널 타입 (알고리즘 클래스용).</summary>
public enum InspectType
{
	InspNone = -1,
	InspBinary,
	InspMatch,
	InspFilter,
	InspAIModule,
	InspCount
}

public enum InspWindowType
{
	None = 0,
	Base,
	Body,
	Sub,
	ID
}

public enum DecisionType
{
	None = 0,
	Good,
	Defect,
	Info,
	Error,
	Timeout
}

public enum eImageChannel : int
{
	None = -1,
	Color,
	Gray,
	Red,
	Green,
	Blue,
	ChannelCount,
}
}
