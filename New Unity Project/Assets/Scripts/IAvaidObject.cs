using UnityEngine;

/// <summary>
/// предоставляет механизм взаимодействия диспетчерской с объектом 
/// </summary>
public interface IAvaidObject
{
	/// <summary>
	/// текущая позиция объекта
	/// </summary>
	Vector3 Position { get; }
	/// <summary>
	/// текущее направление движения объекта
	/// </summary>
	Vector3 Direction { get; }

	/// <summary>
	/// текущая линейная скорость движения объекта, может быть отрицательной
	/// </summary>
	float LinearSpeed { get; }
	/// <summary>
	/// текущая угловая скорость движения объекта
	/// </summary>
	float AngularSpeed { get; }

	/// <summary>
	/// средняя линейная скорость движения объекта, всегда положительна
	/// </summary>
	float AvgLinearSpeed { get; }
	/// <summary>
	/// максимальная линейная сорость движения объекта, всегда положительна
	/// </summary>
	float MaxLinearSpeed { get; }
	/// <summary>
	/// максимальная угловая скорость движения объекта, всегда положительна
	/// </summary>
	float MaxAngularSpeed { get; }
	/// <summary>
	/// может ли объект выполнять моневры ухода от столкновения
	/// </summary>
	bool CanAvaid { get; }
	/// <summary>
	/// точка избежания столкновения, если null - объект никого не избегает
	/// </summary>
	Vector3? Target { get; set; }
}
