using System;
using System.Collections.Generic;
using System.Linq;

namespace PackVisionApp.Core;

/// <summary>
/// 최근 몇 번의 읽기를 저장한 뒤, 정규화 키(같은 값으로 묶는 문자열) 기준으로
/// 가장 많이 등장한 원문(raw)을 고릅니다.
/// <para/>
/// 왜 쓰나: OCR/바코드 디코더는 조명·초점 때문에 매 프레임 글자가 살짝 달라질 수 있습니다.
/// "완전 일치 12번"보다 "최근 5번 중 과반수"가 이해·디버깅이 쉽고 흔들림이 줄어듭니다.
/// </summary>
public sealed class SimpleMajorityBuffer
{
	private readonly int _capacity;
	private readonly List<(string Key, string Raw)> _items = new();

	public SimpleMajorityBuffer(int capacity = 5)
	{
		_capacity = Math.Max(1, capacity);
	}

	public void Clear() => _items.Clear();

	/// <summary>성공한 읽기만 넣습니다. 빈 키는 무시합니다.</summary>
	public void Add(string normalizedKey, string rawDisplay)
	{
		if (string.IsNullOrWhiteSpace(normalizedKey))
			return;

		_items.Add((normalizedKey, rawDisplay ?? string.Empty));
		while (_items.Count > _capacity)
			_items.RemoveAt(0);
	}

	/// <summary>
	/// 버퍼에 값이 있으면, 가장 많이 나온 키에 해당하는 raw 중 "가장 마지막" 것을 돌려줍니다.
	/// (동률이면 리스트에서 더 뒤에 나온 키가 이깁니다 = 최근 쪽 우선.)
	/// </summary>
	public bool TryGetMajorityRaw(out string rawDisplay)
	{
		rawDisplay = string.Empty;
		if (_items.Count == 0)
			return false;

		var bestGroup = _items
			.GroupBy(x => x.Key)
			.OrderByDescending(g => g.Count())
			.ThenByDescending(g => LastIndexOfKey(g.Key))
			.First();

		rawDisplay = bestGroup.Last().Raw;
		return true;
	}

	private int LastIndexOfKey(string key)
	{
		for (int i = _items.Count - 1; i >= 0; i--)
		{
			if (_items[i].Key == key)
				return i;
		}
		return -1;
	}

	/// <summary>lblDebug나 로그에 붙이기 좋은 한 줄 요약.</summary>
	public string FormatKeysForDebug()
	{
		if (_items.Count == 0)
			return "(비어 있음)";
		return string.Join(" → ", _items.Select(x => x.Key));
	}
}
