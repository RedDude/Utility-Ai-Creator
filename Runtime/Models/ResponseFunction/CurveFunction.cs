using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// A freeform response: the shape is an AnimationCurve, edited with Unity's native curve editor.
/// The keys are stored in the "Keys" string parameter ("time,value,inTangent,outTangent;..."),
/// so it is saved, copied and restored like every other function.
/// </summary>
public class CurveFunction : ResponseFunction
{
    private const string KeysParameter = "Keys";

    private AnimationCurve cachedCurve;
    private string cachedKeys;

    public CurveFunction() : base(TypeToName.RF_Curve)
    {
        ParameterContainer.AddParameter(KeysParameter, Serialize(DefaultCurve()));
    }

    /// <summary>The curve the function currently follows (x and y run from 0 to 1).</summary>
    public AnimationCurve GetCurve()
    {
        var keys = ParameterContainer.GetParamString(KeysParameter).Value;
        if (cachedCurve == null || cachedKeys != keys)
        {
            cachedCurve = Parse(keys);
            cachedKeys = keys;
        }
        return cachedCurve;
    }

    public void SetCurve(AnimationCurve curve)
    {
        ParameterContainer.GetParamString(KeysParameter).Value = Serialize(curve);
    }

    protected override float CalculateResponseInternal(float x)
    {
        return GetCurve().Evaluate(x);
    }

    private static AnimationCurve DefaultCurve()
    {
        return new AnimationCurve(new Keyframe(0f, 0f, 1f, 1f), new Keyframe(1f, 1f, 1f, 1f));
    }

    private static string Serialize(AnimationCurve curve)
    {
        var builder = new StringBuilder();
        foreach (var key in curve.keys)
        {
            if (builder.Length > 0) builder.Append(';');
            builder.Append(key.time.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(key.value.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(key.inTangent.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(key.outTangent.ToString("R", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    private static AnimationCurve Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return DefaultCurve();

        var keys = new List<Keyframe>();
        foreach (var part in text.Split(';'))
        {
            var numbers = part.Split(',');
            if (numbers.Length != 4) return DefaultCurve();

            if (!float.TryParse(numbers[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var time) ||
                !float.TryParse(numbers[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                !float.TryParse(numbers[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var inTangent) ||
                !float.TryParse(numbers[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var outTangent))
            {
                return DefaultCurve();
            }
            keys.Add(new Keyframe(time, value, inTangent, outTangent));
        }
        return keys.Count == 0 ? DefaultCurve() : new AnimationCurve(keys.ToArray());
    }
}
