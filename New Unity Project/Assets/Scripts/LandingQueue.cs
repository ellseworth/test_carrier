using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// реализует упавление очередью на посадку
/// </summary>
public class LandingQueue
{
	private readonly LinkedList<IAvaidObject> _order = new LinkedList<IAvaidObject>();
	private readonly Dictionary<IAvaidObject, LinkedListNode<IAvaidObject>> _distanceCache =
		new Dictionary<IAvaidObject, LinkedListNode<IAvaidObject>>();

	/// <summary>
	/// возвращает позицию в очереди на посадку, или null, если можно садиться
	/// </summary>
	/// <exception cref="ArgumentNullException"></exception>
	public Vector3? this[IAvaidObject avaider]
	{
		get
		{
			if (avaider == null)
				throw new ArgumentNullException("avaider");
			//если в череди никого нет, можно садиться
			if (_order.Count == 0)
				return null; 

			LinkedListNode<IAvaidObject> search;
			//если avaider есть в очереди, то будем считать его позицию от предыдущего,
			//в противном случае - от последнего, если таковой есть
			if (!_distanceCache.TryGetValue(avaider, out search))
				search = _order.Last;
			if (search == null || search.Previous == null || search.Previous.Value == null)
				return null;

			//определяем радиус моневра avaider'а, будет использовано, как расстояние до впереди идущего
			float maneurRadius =
				AngularMath.CircleRadius(avaider.MaxLinearSpeed, avaider.MaxAngularSpeed);
			IAvaidObject previous = search.Previous.Value;
			return previous.Position - previous.Direction * maneurRadius;
		}
	}

	public ushort Count { get { return (ushort)_distanceCache.Count; } }

	/// <summary>
	/// попытаться поставить avader'а в очередь, с возвратом его позиции
	/// </summary>
	/// <param name="avaider">кандидат в очередь</param>
	/// <param name="position">позиция в очереди</param>
	/// <returns>true - если добавлен в очередь, иначе  false</returns>
	/// <exception cref="ArgumentNullException"></exception>
	public bool Register(IAvaidObject avaider, out Vector3? position)
	{
		if (avaider == null)
			throw new ArgumentNullException("avaider");

		//определяем адиус виража, как расстояни до впереди идущего
		float maneurRadius =
			AngularMath.CircleRadius(avaider.MaxLinearSpeed, avaider.MaxAngularSpeed);

		LinkedListNode<IAvaidObject> search;
		//ищем позицию кандидата в очереди, или добавляем в конец
		bool result = !_distanceCache.TryGetValue(avaider, out search);
		if (result)
		{
			search = _order.AddLast(avaider);
			_distanceCache[avaider] = search;
		}

		//если нет никого впереди
		if (search.Previous == null || search.Previous.Value == null)
			position = null;
		//если впереди идущий есть, отсчитываем позицию до него
		else
		{
			IAvaidObject previous = search.Previous.Value;
			position = previous.Position - previous.Direction * maneurRadius;
		}
		return result;
	}
	/// <summary>
	/// удаляем avaider'а из очереди
	/// </summary>
	/// <param name="avaider">кандидат на удаление</param>
	/// <returns>true - если удале, иначе false</returns>
	public bool Unregister(IAvaidObject avaider)
	{
		return _distanceCache.Remove(avaider) | _order.Remove(avaider);
	}
}
