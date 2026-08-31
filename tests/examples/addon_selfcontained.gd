extends SceneTree

# The terrain engine must be a REAL addon: copy addons/beep_game_builder_cs into
# any other project and everything still resolves.
#
# Three ways that quietly stops being true, all of which had actually happened:
#
#   1. An absolute disk path. Twenty-three references to C:/Users/f_ald/... sat
#      inside the addon's own scenes. They worked perfectly here and were dead
#      paths anywhere else, and nothing reported it because the art loaded fine
#      on the machine that authored it.
#   2. The addon reaching into tests/. Tests may depend on the addon; the addon
#      may never depend on tests, because tests do not ship.
#   3. Feature code living in tests/examples. Six controllers were there, each
#      hand-listed back into the .csproj - example code compiled as product code,
#      and absent from the thing a consumer actually copies.

const ADDON := "res://addons/beep_game_builder_cs/"

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

# Every file under a directory, recursively, with one of the given extensions.
func files_under(dir: String, extensions: Array) -> Array[String]:
	var found: Array[String] = []
	var pending: Array[String] = [dir]
	while not pending.is_empty():
		var at: String = pending.pop_back()
		for sub in DirAccess.get_directories_at(at):
			pending.append(at + sub + "/")
		for name in DirAccess.get_files_at(at):
			for ext in extensions:
				if name.ends_with(ext):
					found.append(at + name)
					break
	return found

func _initialize() -> void:
	var sources := files_under(ADDON, [".cs", ".tscn", ".tres", ".gd"])
	check(sources.size() > 100, "the addon has sources to check (%d)" % sources.size())

	var absolute: Array[String] = []
	var reaches_into_tests: Array[String] = []

	for path in sources:
		var text := FileAccess.get_file_as_string(path)
		if text.is_empty():
			continue
		var short := path.replace(ADDON, "")

		# A drive letter followed by a colon and a slash, inside a quoted string.
		for line in text.split("\n"):
			var quote := line.find("\"")
			while quote != -1:
				var close := line.find("\"", quote + 1)
				if close == -1:
					break
				var literal := line.substr(quote + 1, close - quote - 1)
				if literal.length() > 3 and literal[1] == ":" and (literal[2] == "/" or literal[2] == "\\"):
					if not absolute.has(short):
						absolute.append(short)
				quote = line.find("\"", close + 1)

		if "res://tests" in text and not reaches_into_tests.has(short):
			reaches_into_tests.append(short)

	check(absolute.is_empty(),
		"no addon file points at an absolute disk path%s"
			% ("" if absolute.is_empty() else ": " + ", ".join(absolute)))
	check(reaches_into_tests.is_empty(),
		"the addon never reaches into tests/%s"
			% ("" if reaches_into_tests.is_empty() else ": " + ", ".join(reaches_into_tests)))

	# Feature code belongs to the addon; tests/ keeps only guards.
	var stray: Array[String] = []
	for name in DirAccess.get_files_at("res://tests/examples/"):
		if name.ends_with(".cs"):
			stray.append(name)
	check(stray.is_empty(),
		"no C# lives in tests/examples%s"
			% ("" if stray.is_empty() else ": " + ", ".join(stray)))

	# And the example scenes ship WITH it, so a consumer gets working wiring.
	var scenes := files_under(ADDON + "templates/scenes/terrain/", [".tscn"])
	check(scenes.size() >= 4,
		"the addon ships its terrain example scenes (%d)" % scenes.size())

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
