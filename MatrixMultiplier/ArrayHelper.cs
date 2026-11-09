namespace Labs;

class ArrayHelper
{
    public static T[]? Split<T>(string s, string separator)
        where T : IParsable<T>
    {
        T[] result = [];
        string part = "";

        s += separator;

        foreach (var c in s)
        {
            if (c.ToString() == separator)
            {
                if (T.TryParse(part, null, out T? value))
                {
                    result = [.. result, value];
                }

                part = "";
            }
            else
            {
                part += c;
            }
        }
        if (result.Length == 0)
        {
            return null;
        }
        return result;
    }
    
    public static string ArrayToString<T>(T[] arr, string separator)
    {
        string result = "";
        foreach (var i in arr)
        {
            result += $"{i}{separator}";
        }

        return result;
    }
    
    public static T[]? Insert<T>(T[] arr, int index, T item)
    {
        T[] result = new T[arr.Length + 1];
        if (index >= result.Length)
        {
            Console.WriteLine("Вставка на индекс превышающий " +
                              "длину списка запрещена");
            return null;
        }
        index = index >= 0 ? index : result.Length + index;
        
        for (int i = 0; i < result.Length; i++)
        {
            if (i < index) { result[i] = arr[i]; }
            else if (i == index) { result[i] = item; }
            else { result[i] = arr[i - 1]; }
        }
        return result;
    }
    
    public static T[]? InsertMany<T>(T[] arr1, int index, T[] arr2)
    {
        T[] result = new T[arr1.Length + arr2.Length];
        if (index > arr1.Length)
        {
            Console.WriteLine("Вставка на индекс превышающий " +
                              "длину списка запрещена");
            return null;
        }
        index = index >= 0 ? index : arr1.Length + 1 + index;
        
        for (int i = 0; i < result.Length; i++)
        {
            if (i < index) { result[i] = arr1[i]; }
            else if (i < index + arr2.Length) { result[i] = arr2[i - index]; }
            else { result[i] = arr1[i - arr2.Length]; }
        }
        return result;
    }
}