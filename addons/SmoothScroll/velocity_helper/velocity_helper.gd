extends Resource
class_name VelocityHelper

## Abstract class

## Attracting strength. The higher the value, the faster it attracts. 
@export_range(0.001, 100000.0, 0.001, "or_greater", "hide_slider")
var attracting_strength := 400.0:
	set(val): attracting_strength= max(val, 0.001)


# Abstract method
func _calculate_velocity_by_time(time: float) -> float:
	return 0.0

# Abstract method
func _calculate_time_by_velocity(velocity: float) -> float:
	return 0.0

# Abstract method
func _calculate_offset_by_time(time: float) -> float:
	return 0.0

# Abstract method
func _calculate_time_by_offset(offset: float) -> float:
	return 0.0


func _calculate_velocity_to_dest(from: float, to: float) -> float:
	var dist = to - from
	var time = _calculate_time_by_offset(abs(dist))
	var vel = _calculate_velocity_by_time(time) * sign(dist)
	return vel


func _calculate_next_velocity(present_time: float, delta_time: float) -> float:
	return _calculate_velocity_by_time(present_time - delta_time)


func _calculate_next_offset(present_time: float, delta_time: float) -> float:
	return _calculate_offset_by_time(present_time) \
		 - _calculate_offset_by_time(present_time - delta_time)


## Return the result of next velocity and position according to delta time
func slide(velocity: float, delta_time: float) -> Array:
	var present_time = _calculate_time_by_velocity(velocity)
	return [
		_calculate_next_velocity(present_time, delta_time),
		_calculate_next_offset(present_time, delta_time)
	]


## Emulate force that attracts something to destination.
## Return the result of next velocity according to delta time
func attract(from: float, to: float, velocity: float, delta_time: float) -> float:
	var dist = to - from
	var target_vel = _calculate_velocity_to_dest(from, to)
	velocity += attracting_strength * dist * delta_time \
		 + _calculate_velocity_by_time(delta_time) * sign(dist)
	if (
		(dist > 0 and velocity >= target_vel) \
		or (dist < 0 and velocity <= target_vel) \
	):
		velocity = target_vel
	return velocity
