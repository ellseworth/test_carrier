using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// организует полет самолетов
/// </summary>
public class CarrierDispatch : MonoBehaviour, IAvaidObject
{
	[Tooltip("компонент передвижения корабля")] [SerializeField] private CarrierMoving _moving;
	[Tooltip("префаб самолета")] [SerializeField] private Plane _planePrefab;
	[Tooltip("количество самолетов")] [SerializeField] private int _planeCount = 5;
	[Tooltip("параметры полета")] [SerializeField] private FlightParams _flightParams;
	[Tooltip("отображение расчетов уклонения")] [SerializeField] private bool _debug;

	private readonly HashSet<IAvaidObject> _onTrack = new HashSet<IAvaidObject>();
	private readonly LandingQueue _landingQueue = new LandingQueue();

	private int _takeoffPending, _scouting;
	private float _takeoffTime;

	public float LinearSpeed { get { return _moving.CurrSpeed; } }
	public float AngularSpeed { get { return _moving.CurrRotateSpeed; } }

	public float AvgLinearSpeed { get { return _moving.MaxLinearSpeed / 2; } }
	public float MaxLinearSpeed { get { return _moving.MaxLinearSpeed; } }
	public float MaxAngularSpeed { get { return _moving.MaxAngularSpeed; } }

	public Vector3 Position { get { return transform.position; } }
	public Vector3 Direction { get { return transform.forward; } }

	public bool CanAvaid { get { return false; } }
	public Vector3? Target
	{
		get { throw new InvalidOperationException("can't get Target from " + GetType().Name); }
		set { throw new InvalidOperationException("can't set Target to " + GetType().Name); }
	}

	private void Awake()
	{
		//провеяем наличие компонентов, и пытаемся найти не достающие
		_moving = _moving ?? GetComponent<CarrierMoving>();
		_flightParams = _flightParams ?? new FlightParams();
		_flightParams.carrier = this;
		_takeoffTime = Time.time - 100;
	}
	private void OnEnable()
	{
		_onTrack.Add(this);
	}
	private void OnDisable()
	{
		_onTrack.Remove(this);
	}

	private void Update ()
	{
		//фиксация желания запуска самолета
		if (Input.GetButtonUp("scout"))
			TryTakeoffPendingPlane();

		//самолет возможно запустить, есть есть самолеты на запуск и при этом не производится посадка
		if (_landingQueue.Count == 0 && _takeoffPending > 0)
		{
			//интервал между запусками/посадкой равен времени полного виража самолета,
			//чтобы при истекании времени полета не создавать скученности самолетов на посадке
			float fullSpinTime = 360 / _planePrefab.MaxAngularSpeed;
			if (Time.time - _takeoffTime > fullSpinTime)
				StartPlane();
		}

		//логика определения угрозы столкновения и расчет точек избегания
		foreach (IAvaidObject avaider in _onTrack)
		{
			//если объект не способен уклоняться, расчитывать нечего
			if (!avaider.CanAvaid)
				continue;
			Vector3 avaiderVelocity = avaider.Direction * avaider.LinearSpeed;
			//итоговый вектор направления избегания столкновения
			Vector3 avaidVector = Vector3.zero;

			//проходимся по каждому из "соперников"
			foreach (IAvaidObject toAvaid in _onTrack)
			{
				if (toAvaid == avaider)
					continue;

				Vector3 toAvaidVelocity = toAvaid.Direction * toAvaid.LinearSpeed;
				//длина проекции скорости и направления движения соперника
				//на скорость и направление движения рассматриваемого объекта
				float projectionLength = Vector3.Dot(avaiderVelocity, toAvaidVelocity) / avaiderVelocity.magnitude;
				//скорректированная скорость рассматриваемого объекта
				float correctedAvaiderLinearSpeed = avaiderVelocity.magnitude - projectionLength;
				//дистанция, на которой стоит предпринимать меы по уклонению начинается с безопасной дистанции полета
				float distanceToReact = _flightParams.saveDistance;
				Vector3 avaiderToAvaid = toAvaid.Position - avaider.Position;
				float maneurRadius;
				//если скорректированная скорость рассматриваемого объекта не положительна,
				//а соперник вне безопасной зоны, можно не учитывать радиус маневрирования
				if (correctedAvaiderLinearSpeed <= 0 && distanceToReact < avaiderToAvaid.magnitude)
					maneurRadius = 0;
				//иначе определяем максимальный радиус маневрирования для рассматриваемого объекта
				else
					maneurRadius =
						AngularMath.CircleRadius(correctedAvaiderLinearSpeed, avaider.MaxAngularSpeed);
				//итоговая дистанция, на которой нужно начинать уворачиваться
				distanceToReact += maneurRadius;
				float fromReactDistanceToAvaid = avaiderToAvaid.magnitude - distanceToReact;
				//если соперник находится за пределами дистанции реагирования, можно не беспокоиться
				if (fromReactDistanceToAvaid > 0)
					continue;
				//иначе добавить к вектору уклонения противоположное направление в той мере,
				//в какой соперник приблизился к рассматриваемому объекту
				else
				{
					if (_debug)
						Debug.DrawLine(avaider.Position, toAvaid.Position, Color.blue);
					avaidVector += avaiderToAvaid.normalized * fromReactDistanceToAvaid;
				}
			}

			//если вектор уворота есть - нужно увернуться
			if (avaidVector != Vector3.zero)
			{
				float maneurRadius =
					AngularMath.CircleRadius(avaider.AvgLinearSpeed, avaider.MaxAngularSpeed);
				avaider.Target = avaider.Position + avaidVector.normalized * maneurRadius;
				if (_debug)
					Debug.DrawRay(avaider.Target.Value, Vector3.up, Color.magenta);
			}
			else
				avaider.Target = null;
		}
	}
	//отображение информации организации полетов
	private void OnGUI()
	{
		string labelPlanes = string.Format(
			"planes | IDLE: {0}, TAKEOFF: {1}, SCOUT: {2}, LANDING: {3}",
			_planeCount.ToString(),
			_takeoffPending.ToString(),
			(_scouting - _landingQueue.Count).ToString(),
			_landingQueue.Count.ToString()
			);
		GUILayout.Label(labelPlanes);
		string deckState = "IDLE";
		if (_landingQueue.Count > 0)
			deckState = "RECEIVING";
		else if (_takeoffPending > 0)
			deckState = "TAKEOFF";
		GUILayout.Label("deck state: " + deckState);
	}

	//пытается запланировать взлет самолета, если есть свободный
	private void TryTakeoffPendingPlane()
	{
		if (_planeCount <= 0)
			return;
		_planeCount--;
		_takeoffPending++;
	}
	//создает и настраивает самолет перед запуском
	private void StartPlane()
	{
		if (_takeoffPending <= 0)
			return;
		_takeoffPending--;
		_scouting++;
		_takeoffTime = Time.time;
		_planePrefab.flight = _flightParams;
		Plane newPlane =
			Instantiate(_planePrefab, transform.position, transform.rotation);
		_planePrefab.flight = null;
		_onTrack.Add(newPlane);
		newPlane.Returned += OnPlaneReturned;
	}
	//выполняет действия по возврату самолета на корабль
	private void OnPlaneReturned(Plane plane)
	{
		_onTrack.Remove(plane);
		_landingQueue.Unregister(plane);
		_planeCount++;
		_scouting--;
		plane.Returned -= OnPlaneReturned;
		_takeoffTime = Time.time;
		Destroy(plane.gameObject);
	}

	/// <summary>
	/// возвращает позицию объекта в очереди на посадку, или null, если очередь свободна
	/// </summary>
	public Vector3? RequestLandingPoint(IAvaidObject avaider)
	{
		return _landingQueue[avaider];
	}
	/// <summary>
	/// пытается зарегистрировать объект в очереди на посадку и возвращает позуцию в очереди,
	/// или null, если очередь свободна
	/// </summary>
	public Vector3? RequestLanding(IAvaidObject avaider)
	{
		Vector3? position;
		_landingQueue.Register(avaider, out position);
		return position;
	}
}
