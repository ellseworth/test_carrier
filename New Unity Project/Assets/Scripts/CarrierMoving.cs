using UnityEngine;

/// <summary>
/// реализует передвижение и управление кораблем
/// </summary>
public class CarrierMoving : MonoBehaviour
{
	[Tooltip("максимальная линейная скорость м/с")] [SerializeField] private float _maxSpeed = 3;
	[Tooltip("ускорение м/с^2")] [SerializeField] private float _acceleration = 0.5f;
	[Tooltip("замедление свободного хода м/с^2")] [SerializeField] private float _sleepDeceleration = 0.1f;
	[Tooltip("максимальная угловая скорость град/с")] [SerializeField] private float _angularSpeed = 2;
	[Tooltip("скорость изменения угловой скорости град/с^2")] [SerializeField] private float _rotateSpeedAcceleration = 1f;
	[Tooltip("отображение расчетов передвижения")] [SerializeField] private bool _debug = false;

	private float _currRotateSpeed;

	public float CurrSpeed { get; private set; }
	public float CurrRotateSpeed { get; private set; }
	public float MaxLinearSpeed { get { return _maxSpeed; } }
	public float MaxAngularSpeed { get { return _angularSpeed; } }

	private void Update ()
	{
		//получаем вектор управления
		Vector2 moveControl = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
		
		//определение ускорения и целевой линейной скорости
		float acceleration, goalValue = 0;
		if (moveControl.y == 0)
			acceleration = _sleepDeceleration * Time.deltaTime;
		else
		{
			acceleration = _acceleration * Time.deltaTime;
			goalValue = moveControl.y * _maxSpeed;
		}
		//корректировка линейной скорости
		CurrSpeed = Mathf.MoveTowards(CurrSpeed, goalValue, acceleration);

		//определение ускорения и угловой скорости
		acceleration = _rotateSpeedAcceleration * Time.deltaTime;
		if (moveControl.x == 0)
			goalValue = 0;
		else
			goalValue = _angularSpeed * moveControl.x;
		//корректировка угловой скорости, не зависящей от линейной скорости
		_currRotateSpeed = Mathf.MoveTowards(_currRotateSpeed, goalValue, acceleration);
		//определение степени влияния лиейной скорости на конечную угловую скорость
		float rotateRate = Mathf.Clamp01(Mathf.Abs(CurrSpeed) / _angularSpeed);
		CurrRotateSpeed = rotateRate * _currRotateSpeed;

		if (_debug)
		{
			for (float forecastTime = Time.deltaTime; forecastTime < 5; forecastTime += Time.deltaTime)
			{
				Quaternion forecastRotation;
				Vector3 forecastPosition = AngularMath.Forecast(
					transform.position,
					transform.forward * CurrSpeed,
					CurrRotateSpeed,
					forecastTime,
					out forecastRotation
					);
				Debug.DrawRay(forecastPosition, Vector3.up, Color.green, 5);
			}
		}

		//применение линейной и угловой скоростей
		transform.position += transform.forward * CurrSpeed * Time.deltaTime;
		transform.Rotate(Vector3.up, CurrRotateSpeed * Time.deltaTime);
	}
}
