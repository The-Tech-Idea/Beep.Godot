#!/usr/bin/env python3
"""Rasterise EPS reference art at full resolution, via the Ghostscript that ships inside GIMP.

WHY
---
Every JPG preview in Example_Art is capped at 1920 wide, while the EPS bounding boxes run to
3000+. The carved edge model (rim 2.05x / bezel 1.14x / shadow 0.76x / plate) was measured off
3-4px bands in those JPEGs -- right at the compression noise floor. Rendering the EPS at 300+ dpi
gives clean, artefact-free pixels for the same measurements.

There is no gs.exe on this machine and no ImageMagick/Inkscape. GIMP 3.2 bundles
`bin/libgs-10.dll`, which exports the standard Ghostscript C API, so ctypes drives it directly --
no GIMP Script-Fu, whose PDB signatures changed between GIMP 2 and 3.

The EPS themselves are licensed third-party reference art. Renders go to tmp/ (gitignored) and
are for MEASUREMENT ONLY -- no vecteezy pixels ship in the addon.

USAGE
    python eps_render.py --all [--dpi 300]
    python eps_render.py <file.eps> [more.eps ...] [--dpi 300]
    python eps_render.py --selftest
"""
import ctypes
import glob
import os
import sys

GIMP_BIN = r"C:\Users\f_ald\AppData\Local\Programs\GIMP 3\bin"
GS_DLL = os.path.join(GIMP_BIN, "libgs-10.dll")

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ART = os.path.join(REPO, "Example_Art")
OUT = os.path.join(REPO, "tmp", "eps")

GS_ARG_ENCODING_UTF8 = 1


class Ghostscript:
    """Minimal RAII wrapper. Each render gets a FRESH instance.

    Ghostscript's API is explicitly single-init: after gsapi_init_with_args + gsapi_exit an
    instance is spent, and reusing one silently produces a truncated or empty second file.
    """

    def __init__(self):
        if not os.path.isfile(GS_DLL):
            raise RuntimeError(f"Ghostscript not found at {GS_DLL}")
        # libgs pulls sibling DLLs out of GIMP's bin; without this the load fails with a
        # bare "DLL load failed" that says nothing about which dependency was missing.
        if hasattr(os, "add_dll_directory") and os.path.isdir(GIMP_BIN):
            self._cookie = os.add_dll_directory(GIMP_BIN)
        self.lib = ctypes.CDLL(GS_DLL)
        self.inst = ctypes.c_void_p()
        rc = self.lib.gsapi_new_instance(ctypes.byref(self.inst), None)
        if rc < 0:
            raise RuntimeError(f"gsapi_new_instance failed: {rc}")
        self.lib.gsapi_set_arg_encoding(self.inst, GS_ARG_ENCODING_UTF8)

    def run(self, args):
        argv = (ctypes.c_char_p * len(args))(*[a.encode("utf-8") for a in args])
        rc = self.lib.gsapi_init_with_args(self.inst, len(args), argv)
        # -101 is gs_error_Quit, which is the NORMAL result of -dBATCH. Treating it as a
        # failure would reject every successful render.
        if rc not in (0, -101):
            raise RuntimeError(f"gsapi_init_with_args failed: {rc}")

    def close(self):
        try:
            self.lib.gsapi_exit(self.inst)
        finally:
            self.lib.gsapi_delete_instance(self.inst)
            if hasattr(self, "_cookie"):
                self._cookie.close()


def render(src, dst, dpi=300):
    """EPS -> PNG. Returns (ok, message). -dEPSCrop honours the bounding box rather than
    padding the art onto a default letter page."""
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    gs = Ghostscript()
    try:
        gs.run([
            "gs", "-dNOPAUSE", "-dBATCH", "-dSAFER", "-dQUIET",
            "-sDEVICE=png16m", "-dEPSCrop",
            "-dTextAlphaBits=4", "-dGraphicsAlphaBits=4",
            f"-r{dpi}", f"-sOutputFile={dst}", src,
        ])
    finally:
        gs.close()
    if not os.path.isfile(dst) or os.path.getsize(dst) == 0:
        return False, "no output written"
    return True, f"{os.path.getsize(dst) / 1e6:.1f} MB"


def dims(path):
    try:
        from PIL import Image
        with Image.open(path) as im:
            return f"{im.size[0]}x{im.size[1]}"
    except Exception:
        return "?"


def selftest():
    """Render a synthesised EPS of known size and require the pixels to match.

    A converter that silently writes a 0-byte or default-letter-size file looks like success
    from the exit code alone; this makes that failure visible before any real art is trusted.
    """
    ok = True
    tmp = os.path.join(OUT, "_selftest")
    os.makedirs(tmp, exist_ok=True)
    eps = os.path.join(tmp, "probe.eps")
    # 200x100pt box with a filled rect. At 72dpi that must come out exactly 200x100.
    with open(eps, "w") as f:
        f.write("%!PS-Adobe-3.0 EPSF-3.0\n%%BoundingBox: 0 0 200 100\n"
                "0.2 0.4 0.9 setrgbcolor 20 20 160 60 rectfill\n%%EOF\n")

    png = os.path.join(tmp, "probe.png")
    good, msg = render(eps, png, dpi=72)
    got = dims(png) if good else "-"
    passed = good and got == "200x100"
    ok &= passed
    print(f"[{'ok ' if passed else 'FAIL'}] 200x100pt @72dpi -> {got} (want 200x100) {msg}")

    png2 = os.path.join(tmp, "probe2x.png")
    good2, _ = render(eps, png2, dpi=144)
    got2 = dims(png2) if good2 else "-"
    passed2 = good2 and got2 == "400x200"
    ok &= passed2
    print(f"[{'ok ' if passed2 else 'FAIL'}] same file @144dpi -> {got2} (want 400x200) "
          f"— proves dpi is honoured and the instance is not stale")

    bad = os.path.join(tmp, "not_a.eps")
    with open(bad, "w") as f:
        f.write("this is not postscript\n")
    try:
        good3, _ = render(bad, os.path.join(tmp, "bad.png"), dpi=72)
        rejected = not good3
    except RuntimeError:
        rejected = True
    ok &= rejected
    print(f"[{'ok ' if rejected else 'FAIL'}] garbage input rejected (want a loud failure, "
          f"not a blank PNG)")

    print("\nSELFTEST", "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    argv = sys.argv[1:]
    dpi = 300
    if "--dpi" in argv:
        i = argv.index("--dpi")
        dpi = int(argv[i + 1])
        del argv[i:i + 2]

    if not argv or argv[0] == "--selftest":
        sys.exit(selftest())

    srcs = sorted(glob.glob(os.path.join(ART, "*.eps"))) if argv[0] == "--all" else argv
    if not srcs:
        print("no input")
        sys.exit(1)

    fails = 0
    print(f"rendering {len(srcs)} file(s) at {dpi} dpi -> {OUT}")
    for s in srcs:
        name = os.path.splitext(os.path.basename(s))[0].replace("vecteezy_", "")
        dst = os.path.join(OUT, name + ".png")
        try:
            good, msg = render(s, dst, dpi)
        except Exception as e:
            good, msg = False, str(e)
        if good:
            print(f"  ok    {name[:56]:<58}{dims(dst):>12}  {msg}")
        else:
            fails += 1
            print(f"  FAIL  {name[:56]:<58}{'':>12}  {msg}")
    print(f"\n{len(srcs) - fails}/{len(srcs)} rendered")
    sys.exit(1 if fails else 0)
