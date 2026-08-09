using Beep.ECS.UI;
using Beep.GameBuilder;
using Godot;

[GlobalClass]
public partial class DataBinderHostSmoke : Node
{
    public string Failure { get; private set; } = string.Empty;

    public bool Run()
    {
        Failure = string.Empty;

        var host = new Control();
        AddChild(host);

        var binder = new DataBinderHostComponent { AutoRefresh = false };
        host.AddChild(binder);

        var info = new GameInfo
        {
            GameName = "Original",
            TargetFps = 60,
            PixelArt = true
        };

        var label = new Label();
        host.AddChild(label);
        if (!Expect(info.GetType().GetProperty(nameof(GameInfo.GameName)) != null, "GameInfo.GameName was not visible to reflection."))
            return false;
        label.Set("text", Variant.From("Direct"));
        if (!Expect(label.Text == "Direct", $"Direct Label.Set('text') failed; label text is '{label.Text}'."))
            return false;
        binder.BindLabel(info, nameof(GameInfo.GameName), label, "Title: {0}");
        if (!Expect(label.Text == "Title: Original", $"BindLabel did not push initial source text; label text is '{label.Text}', property value is '{info.GameName}'."))
            return false;
        info.GameName = "Changed";
        binder.RefreshAll();
        if (!Expect(label.Text == "Title: Changed", "RefreshAll did not update one-way label binding."))
            return false;

        var bar = new ProgressBar { MaxValue = 200 };
        host.AddChild(bar);
        binder.BindProgress(info, nameof(GameInfo.TargetFps), bar);
        if (!Expect(Mathf.IsEqualApprox((float)bar.Value, 60f), "BindProgress did not push initial number."))
            return false;
        info.TargetFps = 144;
        binder.RefreshAll();
        if (!Expect(Mathf.IsEqualApprox((float)bar.Value, 144f), $"RefreshAll did not update progress bar; bar value is {bar.Value}, source value is {info.TargetFps}."))
            return false;

        var twoWay = new CheckBox();
        host.AddChild(twoWay);
        info.PixelArt = true;
        binder.BindCheckBox(info, nameof(GameInfo.PixelArt), twoWay, Beep.ECS.UI.BindingMode.TwoWay);
        if (!Expect(twoWay.ButtonPressed, "TwoWay checkbox did not initialize from source."))
            return false;
        twoWay.ButtonPressed = false;
        binder.RefreshTwoWay();
        if (!Expect(info.PixelArt == false, "TwoWay checkbox did not write target back to source."))
            return false;

        var oneWayToSource = new CheckBox { ButtonPressed = false };
        host.AddChild(oneWayToSource);
        info.PixelArt = true;
        binder.Bind(info, nameof(GameInfo.PixelArt), oneWayToSource, "ButtonPressed", Beep.ECS.UI.BindingMode.OneWayToSource);
        if (!Expect(info.PixelArt == false, "OneWayToSource bind did not pull initial target value."))
            return false;
        oneWayToSource.ButtonPressed = true;
        binder.RefreshAll();
        if (!Expect(info.PixelArt == false, "RefreshAll pushed OneWayToSource in the wrong direction."))
            return false;
        binder.RefreshTwoWay();
        if (!Expect(info.PixelArt, "RefreshTwoWay did not pull OneWayToSource target value."))
            return false;

        host.QueueFree();
        return true;
    }

    private bool Expect(bool condition, string failure)
    {
        if (condition)
            return true;

        Failure = failure;
        return false;
    }
}
