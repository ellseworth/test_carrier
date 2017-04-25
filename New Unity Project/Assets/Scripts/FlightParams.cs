using System;
using UnityEngine;

/// <summary>
/// предоставляет параметры полета для самолетов
/// </summary>
[Serializable]
public class FlightParams
{
	[Tooltip("минимальная дистанция сближения с другими объектами м")]
	public float saveDistance = 20;
	[Tooltip("дистанция разведки от корабля м")]
	public float scoutDistance = 100;
	[Tooltip("время разведки с")]
	public float scoutTime = 30;
	[HideInInspector]
	public CarrierDispatch carrier;
}
