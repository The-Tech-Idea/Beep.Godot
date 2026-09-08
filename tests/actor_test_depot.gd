extends Node

var amounts := {}

func CanAccept(_resource_id: String) -> bool:
	return true

func Load(resource_id: String, amount: int) -> int:
	amounts[resource_id] = int(amounts.get(resource_id, 0)) + amount
	return amount

func Stored(resource_id: String) -> int:
	return int(amounts.get(resource_id, 0))
