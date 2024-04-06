extends VelocityUtility
class_name ExpoVelocityUtility


## Friction, not physical. 
## The higher the value, the more obvious the deceleration. 
@export_range(1.001, 10000.0, 0.001, "or_greater", "hide_slider")
var friction := 10000.0:
	set(val): friction = max(val, 1.001)

## Minumun velocity.
@export_range(0.001, 100000.0, 0.001, "or_greater", "hide_slider")
var minimum_velocity := 0.4:
	set(val): minimum_velocity = max(val, 0.001)


func _calculate_velocity_by_time(time: float) -> float:
	var minimum_time = _calculate_time_by_velocity(minimum_velocity)
	if time <= minimum_time: return 0.0
	return pow(friction, time)


func _calculate_time_by_velocity(velocity: float) -> float:
	return log(abs(velocity)) / log(friction)


func _calculate_offset_by_time(time: float) -> float:
	return pow(friction, time) / log(friction)


func _calculate_time_by_offset(offset: float) -> float:
	return log(offset * log(friction)) / log(friction)


func _calculate_velocity_to_dest(from: float, to: float) -> float:
	var dist = to - from
	var min_time = _calculate_time_by_velocity(minimum_velocity)
	var min_offset = _calculate_offset_by_time(min_time)
	var time = _calculate_time_by_offset(abs(dist) + min_offset)
	var vel = _calculate_velocity_by_time(time) * sign(dist)
	return vel
