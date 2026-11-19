using System;
using UnityEngine;

public static class JsonArrayHelper
{
    [Serializable]
    private class Wrapper<T>
    {
        public T[] items;
    }

    public static T[] FromJsonArray<T>(string json)
    {
        string wrappedJson = "{\"items\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrappedJson);
        return wrapper.items;
    }

    public static T[] FromJson<T>(string json)
    {
        return FromJsonArray<T>(json);
    }
}