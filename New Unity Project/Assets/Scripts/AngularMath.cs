using System;
using UnityEngine;

public static class AngularMath
{
	public static float CircleRadius(float linearSpeedMpSec, float angularSpeedDegpSec)
	{
		if (angularSpeedDegpSec == 0)
			throw new ArgumentException("angularSpeedDegpSec == 0");
		float spinPerSec = angularSpeedDegpSec / 360;
		float radianPerSec = spinPerSec * 2 * Mathf.PI;
		return Mathf.Abs(linearSpeedMpSec / radianPerSec);
	}
	public static Vector3 Forecast(
		Ray from, float angularSpeed, float timeSec, out Quaternion rotation
		)
	{
		return Forecast(from.origin, from.direction, angularSpeed, timeSec, out rotation);
	}
	public static Vector3 Forecast(
		Vector3 position, Vector3 linearSpeed, float angularSpeed, float timeSec, out Quaternion rotation
		)
	{
		Vector3 result;
		if (angularSpeed == 0)
		{
			result = position + linearSpeed * timeSec;
			rotation = Quaternion.Euler(0, 0, 0);
			return result;
		}

		float radiusLength;
		Vector3 circleCenter = CircleCenter(position, linearSpeed, angularSpeed, out radiusLength);
		Vector3 centerToPos = position - circleCenter;

		float degForTime = angularSpeed * timeSec;
		rotation = Quaternion.AngleAxis(degForTime, Vector3.up);

		Vector3 newCenterToPos = rotation * centerToPos;
		result = circleCenter + newCenterToPos;
		return result;
	}
	public static Vector3 CircleCenter(
		Vector3 position, Vector3 linearSpeed, float angularSpeed, out float radius
		)
	{
		if (angularSpeed == 0)
			throw new ArgumentException("angularSpeed == 0");

		radius = CircleRadius(linearSpeed.magnitude, angularSpeed);

		Vector3 radiusDirection = Vector3.Cross(linearSpeed.normalized, Vector3.up).normalized;
		if (angularSpeed > 0)
			radiusDirection = -radiusDirection;
		radiusDirection = radiusDirection.normalized * radius;

		Vector3 circleCenter = position + radiusDirection;
		return circleCenter;
	}
}
