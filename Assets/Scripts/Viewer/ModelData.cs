using System.Collections.Generic;

public class ModelDataRoot
{
    public Dictionary<string, ModelInfo> models;
    public string coordinate_system;
    public string units;
}

public class ModelInfo
{
    public string status;
    public double[] position;
    public double[] position_relative_to_brain;
    public double distance_from_brain_center_mm;
    public ScaleInfo scale;
}

public class ScaleInfo
{
    public double x;
    public double y;
    public double z;
}
