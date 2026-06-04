using System.Collections.Generic;

public class ModelDataRoot
{
    public Dictionary<string, ModelInfo> models;
    public string coordinate_system;
    public string units;
    public IncisionQuizData incision_quiz;
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

public class IncisionQuizData
{
    public float point_radius_mm;
    public int correct_point_index;
    public string correct_explanation;
    public string wrong_explanation;
    public List<IncisionPointData> points;
}

public class IncisionPointData
{
    public int index;
    public string label;
    public double[] direction;
    public double[] position;
    public bool is_correct;
    public string note;
}
