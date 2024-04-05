extends VelocityHelper
class_name LinearVelocityHelper


## Friction, not physical. 
## The higher the value, the more obvious the deceleration. 
@export_range(0.001, 100000.0, 0.001, "or_greater", "hide_slider")
var friction := 10000.0:
	set(val): friction = max(val, 0.001)


func _calculate_velocity_by_time(time: float) -> float:
	if time <= 0.0: return 0.0
	return time * friction


func _calculate_time_by_velocity(velocity: float) -> float:
	return abs(velocity) / friction


func _calculate_offset_by_time(time: float) -> float:
	time = max(time, 0.0)
	return 1.0/2.0 * friction * time*time


func _calculate_time_by_offset(offset: float) -> float:
	return sqrt(abs(offset) * 2.0 / friction)
