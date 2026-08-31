extends SceneTree
func _initialize() -> void:
	var n := FastNoiseLite.new()
	n.seed = 12345 + 33427
	n.noise_type = FastNoiseLite.TYPE_PERLIN
	n.fractal_type = FastNoiseLite.FRACTAL_FBM
	n.fractal_octaves = 5
	n.fractal_lacunarity = 2.0
	n.fractal_gain = 0.48
	for freq in [0.01, 0.0066]:
		n.frequency = freq
		var vals := []
		for y in range(60):
			for x in range(96):
				vals.append(n.get_noise_2d(x + 0.5, y + 0.5))
		vals.sort()
		var c := vals.size()
		print("freq %.4f  min %.3f  p10 %.3f  p50 %.3f  p90 %.3f  max %.3f" %
			[freq, vals[0], vals[c/10], vals[c/2], vals[c*9/10], vals[c-1]])
	quit(0)
