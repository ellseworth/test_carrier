using System;
using UnityEngine;

/// <summary>
/// реализует логику самолета - разведчика
/// </summary>
public class Plane : MonoBehaviour, IAvaidObject
{
	[Tooltip("максимальная линейная скорость м/с")] [SerializeField] private float _maxSpeed = 30;
	[Tooltip("минимальная линейная скорость м/с")] [SerializeField] private float _minSpeed = 20;
	[Tooltip("ускорение/замедление м/с^2")] [SerializeField] private float _acceleration = 10;
	[Tooltip("угловая скорость град/с")] [SerializeField] private float _angularSpeed = 10;
	[Tooltip("погрешность определения нахождения в точке м")] [SerializeField] private float _sleepDistance = 0.5f;
	[Tooltip("графическое отображение расчетов движения")] [SerializeField] private bool _debug;

	//состояние самолета
	private byte _state;
	//итоговая цель полета
	private Vector3? _target;
	//время взлета
	private float _startTime;

	//параметры полета
	[HideInInspector] public FlightParams flight;

	public Vector3 Position
	{
		get { return transform.position; }
		private set { transform.position = value; }
	}
	public Vector3 Direction { get { return transform.forward; } }

	public float LinearSpeed { get; private set; }
	public float AngularSpeed { get; private set; }

	public float AvgLinearSpeed { get { return (_minSpeed + _maxSpeed) / 2; } }
	public float MaxLinearSpeed { get { return _maxSpeed; } }
	public float MaxAngularSpeed { get { return _angularSpeed; } }

	public bool CanAvaid { get { return _state < 2; } }
	public Vector3? Target { get; set; }

	/// <summary>
	/// вызывается, когда самолет вернулся на корабль
	/// </summary>
	public event Action<Plane> Returned;

	private void Start()
	{
		_startTime = Time.time;
		LinearSpeed = _minSpeed;
	}
	private void Update()
	{
		CarrierDispatch carrier = flight.carrier;
		Color debugTargetColor = Color.green;
		switch (_state)
		{
			//стандарьный режим разведки
			case 0:
				GetStartLandingPoint();
				debugTargetColor = Color.green;
				//определяем и оцениваем оставшееся время разведки
				float restTime = flight.scoutTime - (Time.time - _startTime);
				if (restTime <= 0)
				{
					_state++;
					break;
				}
				//определяем по приоритетм цель, к которой нужно лететь
				_target = Target ?? _target ?? GetNextScoutPoint();
				//определяем достижимость цели с корректировкой по скорости
				//если цель назначена диспетчерской, ее нельзя отменить
				float linearSpd = LinearSpeed;
				if (!CanBeReached(_target.Value, ref linearSpd) && Target == null)
					_target = null;
					LinearSpeed = linearSpd;
				break;
			//режим ожидания очереди на посадку
			case 1:
				debugTargetColor = Color.yellow;
				//запрашиваем у диспетчерской свое место в очереди на посадку
				Vector3? prepairLandingPoint = carrier.RequestLanding(this);
				//если места нет, значит можно сажать
				if (prepairLandingPoint == null)
				{
					_state++;
					break;
				}
				//находясь в очереди, желательно по-прежнему избегать столкновений, поэтому слушаем диспетчерскую
				_target = Target ?? prepairLandingPoint;
				//определяем достижимость цели с корректировкой по скорости
				linearSpd = LinearSpeed;
				CanBeReached(_target.Value, ref linearSpd);
				LinearSpeed = linearSpd;
				break;
			//режим посадки
			case 2:
				debugTargetColor = Color.red;
				//если находимся в точке посадки, с допустимым направлением - считай сели
				if (IsInPoint(carrier.Position, 0) && Vector3.Angle(Direction, carrier.Direction) < 30)
				{
					if (Returned != null)
						Returned(this);
					Destroy(gameObject);
					break;
				}
				//следуем к точке на траектории посадки
				_target = GetStartLandingPoint();
				linearSpd = LinearSpeed;
				//определяем достижимость цели с корректировкой по скорости
				CanBeReached(_target.Value, ref linearSpd);
				LinearSpeed = linearSpd;
				break;
		}

		//логика движения к цели===========================
		//поворот к цели
		if (_target.HasValue)
		{
			Vector3 toTarget = _target.Value - Position;
			Quaternion toTargetRotation =
				toTarget == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(toTarget);

			//сохранеие текущей угловой скорости для пользования ей другими компонентами
			//по довороту до цели определяем знак угловой скорости
			Quaternion fromToRotation = Quaternion.FromToRotation(Direction, toTarget);
			if (fromToRotation.eulerAngles.y == 0)
				AngularSpeed = 0;
			else if (fromToRotation.eulerAngles.y < 0)
				AngularSpeed = -_angularSpeed;
			else
				AngularSpeed = _angularSpeed;

			//собственно, поворот к цели
			transform.rotation = Quaternion.RotateTowards(
				transform.rotation,
				toTargetRotation,
				_angularSpeed * Time.deltaTime
				);

			if (_debug)
				Debug.DrawLine(Position, _target.Value, debugTargetColor);
		}

		//движение вперед
		float deltaPos = LinearSpeed * Time.deltaTime;
		Vector3 deltaPosition = Direction * deltaPos;
		if (deltaPosition != Vector3.zero)
			Position += deltaPosition;
		//сбросить цель, ели она достигнута
		if (_target.HasValue && IsInPoint(_target.Value, deltaPos))
			_target = null;
	}

	//определение, находится ли checker в точке point с учетом скорости движения deltaPosition
	private bool IsInPoint(Vector3 point, Vector3 checker, float deltaPosition)
	{
		if (deltaPosition <= 0)
			deltaPosition = LinearSpeed * Time.deltaTime;

		float sleepDistance = Mathf.Max(deltaPosition, _sleepDistance);
		return Vector3.Distance(point, checker) <= sleepDistance;
	}
	//определение, находится ли самолет в точке point с учетом скорости движения deltaPosition
	private bool IsInPoint(Vector3 point, float deltaPosition)
	{
		return IsInPoint(point, Position, deltaPosition);
	}
	//определение, может ли самолет повернуть в указанную точку без дополнительных виражей
	private bool CanBeReached(Vector3 position, ref float speed)
	{
		Vector3 toPosition = position - Position;
		Vector3 toPositionLocal = transform.InverseTransformDirection(toPosition);

		//если точка находится почти на прямой перед нами - можно смело лететь
		if (toPositionLocal.z >= 0 && Mathf.Abs(toPositionLocal.x) < 0.0001f)
		{
			speed = CorrectSpeed(speed, _maxSpeed);
			return true;
		}

		//определение по относительному положению точки угловой скорости со знаком, чтобы довернуть до нее
		float angSpeed = _angularSpeed;
		if (toPositionLocal.x < 0)
			angSpeed = -_angularSpeed;

		float maneurRadius;
		//определяем центр радиуса виража, и сам радиус при минимальной скорости
		Vector3 maneurCenter = AngularMath.CircleCenter(
			Position, Direction * _minSpeed, angSpeed, out maneurRadius
			);
		if (_debug)
			Debug.DrawRay(maneurCenter, (Position - maneurCenter).normalized * maneurRadius, Color.cyan);

		float fromCenterToPosition = Vector3.Distance(position, maneurCenter);
		//если расстояние от центра виража до самолема меньше радиуса,
		//то точка находится внутри виража, и достичь ее не можем
		if (fromCenterToPosition < maneurRadius)
		{
			speed = CorrectSpeed(speed, _minSpeed);
			return false;
		}

		//точка находится где-то за пределами виража,
		//нужно расчитать скорость, на которой самолет сможет в нее попасть
		float radiusRate = fromCenterToPosition / maneurRadius;
		float moneurMaxSpeed = radiusRate * _minSpeed;
		speed = CorrectSpeed(speed, moneurMaxSpeed);
		return true;
	}
	//плавно изменяет скорость, с учетом ускорения
	private float CorrectSpeed(float currSpeed, float targetSpeed)
	{
		targetSpeed = Mathf.Clamp(targetSpeed, _minSpeed, _maxSpeed);
		return Mathf.MoveTowards(currSpeed, targetSpeed, _acceleration * Time.deltaTime);
	}
	//расчитывает примерное время, необходимое самолету, чтобы прилететь к кораблю
	private float ToCarrierTime()
	{
		CarrierDispatch carrier = flight.carrier;
		//определяем направление до корабля
		Vector3 toCarrier = carrier.Position - Position;
		float forecastTime = 0;
		
		float toCarrierAngle = Vector3.Angle(toCarrier, Direction);
		//определяем время, необходимое, чтобы лечь на курс до корабля
		forecastTime += toCarrierAngle / _angularSpeed;

		//если до корабля не нулевое расстояние, нужно посчитать, за сколько его преодолеет самолет
		if (toCarrier.magnitude > 0)
		{
			//расчитываем длину проекции скорости и направления движения корабля на курс до корабля,
			//если он от нас сматывается, то лететь придется  дольше
			float carrierCourseProjection =
				Vector3.Dot(carrier.Direction * carrier.LinearSpeed, toCarrier.normalized);
			float speedToReachCarrier = LinearSpeed - carrierCourseProjection;
			//если получившаяся со всеми компенсациями скорость не положительна, то мы корабль не догоним никогда
			if (speedToReachCarrier <= 0)
				return float.MaxValue;

			forecastTime += toCarrier.magnitude / speedToReachCarrier;
		}

		return forecastTime;
	}
	//прогноз позиции корабя на время, которое нужно, чтобы до него долететь
	private Vector3 ForecastCarrierPosition(out float forecastTimeSec)
	{
		CarrierDispatch carrier = flight.carrier;
		//время, чтобы прилететь к кораблю
		forecastTimeSec = ToCarrierTime();

		Quaternion forecastRotation;
		//находим саму прогнозируемую позицию
		Vector3 carrierForecastPosition = AngularMath.Forecast(
			carrier.Position,
			carrier.Direction * carrier.LinearSpeed,
			carrier.AngularSpeed,
			forecastTimeSec,
			out forecastRotation
			);
		return carrierForecastPosition;
	}
	//поиск следующей рандомной точки для разведки
	private Vector3 GetNextScoutPoint()
	{
		float forecastTime;
		//поиск прогнозируемой позиции корабля, на время, которое нужно, чтобы до него долететь
		Vector3 carrierForecastPosition = ForecastCarrierPosition(out forecastTime);
		//случайная точка разведки, относительно корабля
		Vector2 random = UnityEngine.Random.insideUnitCircle.normalized * flight.scoutDistance;
		//в итоге получаем случайную точку от прогнозируемой позиции корабля
		Vector3 result = carrierForecastPosition + new Vector3(random.x, 0, random.y);
		result.y = Position.y;
		return result;
	}
	//находит точку захода посадку
	//чем более "правильное" положение занимает самолет относительно корабля, тем точка ближе к месту посадки,
	//"доводя" таким образом самолет до нужного направления захода на палубу
	private Vector3 GetStartLandingPoint()
	{
		CarrierDispatch carrier = flight.carrier;

		//сначала рассчитываем время прогноза позиции корабля
		float toCarrierDistance = Vector3.Distance(Position, carrier.Position);
		float toCarrierTime = toCarrierDistance / _minSpeed;
		float toCarrierDirectionAngle = Vector3.Angle(carrier.Direction, Direction);
		float toCarrierDirectionTime = toCarrierDirectionAngle / _angularSpeed;
		float forecastTime = toCarrierTime + toCarrierDirectionTime;
		//на основе времени прогноза расчитываем направление и позуцию корабля
		Quaternion forecastRotation;
		Vector3 forecastCarrierPosition = AngularMath.Forecast(
			carrier.Position,
			carrier.Direction * -Mathf.Abs(carrier.LinearSpeed),
			carrier.AngularSpeed,
			forecastTime,
			out forecastRotation
			);

		//расчитываем величину доворота курса до прогнозируемой позиции корабля до прогнозируемого направления корабля,
		Vector3 forecastCarrierDirection = forecastRotation * carrier.Direction;
		Vector3 toForecastCarrierPosition = forecastCarrierPosition - Position;
		//это даст понимание расположения самолета относительно корабля в его прогнозируемой позиции
		Vector3 fromToCarrierToForecastDirectionEuler =
			Quaternion.FromToRotation(toForecastCarrierPosition, forecastCarrierDirection).eulerAngles -
			new Vector3(0, 180, 0);

		//на основе предыдущего расчета, определяем, в какую сторону должен будет поворачивать самолет
		float signedAngSpeed = _angularSpeed;
		if (fromToCarrierToForecastDirectionEuler.y > 0)
			signedAngSpeed = -signedAngSpeed;
		//коррекция угловой скорости самолета на угловую скорость корабля
		if (carrier.LinearSpeed > 0)
			signedAngSpeed += Mathf.Max(carrier.AngularSpeed, 0);
		else if (carrier.LinearSpeed < 0)
			signedAngSpeed -= Mathf.Min(carrier.AngularSpeed, 0);

		float maneurRadius;
		//расчет центра маневра от прогнозирумых позиции и нарпавления корабля
		Vector3 maneurCenter = AngularMath.CircleCenter(
			forecastCarrierPosition,
			forecastCarrierDirection * LinearSpeed,
			signedAngSpeed,
			out maneurRadius
			);
		
		if (_debug)
		{
			Vector3 radius = Vector3.forward * maneurRadius;
			Quaternion oneGeg = Quaternion.AngleAxis(1, Vector3.up);
			for (int i = 0; i < 360; i++)
			{
				Vector3 debugPoint = maneurCenter + radius;
				Debug.DrawRay(debugPoint, Vector3.up, Color.white);
				radius = oneGeg * radius;
			}
		}

		//строим гопотенузу прямого треугольника для расчета касательной
		Vector3 toManeurCenter = maneurCenter - Position;
		//расчет длины касатльной от позиции самолета к траектории виража на посадку
		float distanceToTangentPoint = Mathf.Pow(toManeurCenter.magnitude, 2) - Mathf.Pow(maneurRadius, 2);
		//если длина касательной не положительна, то мы находимся внутри траектории виража,
		//надо отлетать от корабля, но можно поропбовать зайти на посадку
		if (distanceToTangentPoint <= 0)
			return carrier.Position;
		distanceToTangentPoint = Mathf.Pow(distanceToTangentPoint, 0.5f);
		
		//строим основу для касательной от гипотенузы (центр виража - позиция самолета)
		Vector3 toManeurTangent = toManeurCenter.normalized * distanceToTangentPoint;
		//определяем угол между гипотенузой и касательной
		float toManeurTangentRotateSin = maneurRadius / toManeurCenter.magnitude;
		float toManeurTangetnRotateAngle = Mathf.Asin(toManeurTangentRotateSin) * 180 / Mathf.PI;
		if (signedAngSpeed > 0)
			toManeurTangetnRotateAngle = -toManeurTangetnRotateAngle;
		//для поворота заготовки касательной
		Quaternion rotateToManeurTangent = Quaternion.AngleAxis(toManeurTangetnRotateAngle, Vector3.up);
		//построение касательной от позиции самолета к траектории входа на посадку
		toManeurTangent = rotateToManeurTangent * toManeurTangent;
		//определние точки входа в траекторию посадки
		return Position + toManeurTangent;
	}
	//определяет самое длительное время, за которое самолет долетит до корабля
	private float GetMaxTimeToCarrier(Vector3? prepairLandingPoint)
	{
		CarrierDispatch carrier = flight.carrier;
		float time = (360 / _angularSpeed);
		float avgSpeed = AvgLinearSpeed - carrier.MaxLinearSpeed;
		if (prepairLandingPoint.HasValue)
		{
			time += Vector3.Distance(Position, prepairLandingPoint.Value) / avgSpeed;
			time += Vector3.Distance(carrier.Position, prepairLandingPoint.Value) / avgSpeed;
		}
		else
			time += Vector3.Distance(carrier.Position, Position) / avgSpeed;
		return time;
	}
}
