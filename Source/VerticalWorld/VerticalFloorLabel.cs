namespace DeepRim.Vertical.VerticalWorld;

public static class VerticalFloorLabel
{
    public static string Format(int levelIndex)
    {
        if (levelIndex > 0)
        {
            return "+" + levelIndex;
        }

        if (levelIndex == 0)
        {
            return "0";
        }

        return levelIndex.ToString();
    }
}
