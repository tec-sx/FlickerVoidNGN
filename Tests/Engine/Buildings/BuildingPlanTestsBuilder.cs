using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Tests;

/// <summary>Plans for the geometry tests, and a check that a solid is closed.</summary>
internal static class BuildingPlanTestsBuilder
{
    /// <summary>A new ground floor with an inside wall from its front to its back at X = 1.</summary>
    public static BuildingStorey WithDividingWall()
    {
        BuildingStorey ground = BuildingFile.Create().Storeys[0];
        ground.Corners.Add(new PlanCorner { Id = 20, X = 1, Z = -2.85f });
        ground.Corners.Add(new PlanCorner { Id = 21, X = 1, Z = 2.85f });

        // The front wall (1 to 2) and the back wall (3 to 4) are split where the inside wall meets them.
        PlanWall front = ground.Walls[0];
        PlanWall back = ground.Walls[2];
        ground.Walls.Add(new PlanWall { Id = 22, From = 20, To = front.To, Outside = true });
        front.To = 20;
        ground.Walls.Add(new PlanWall { Id = 23, From = 21, To = back.To, Outside = true });
        back.To = 21;
        ground.Walls.Add(new PlanWall { Id = 24, From = 20, To = 21 });
        ground.Openings.Clear();
        return ground;
    }

    /// <summary>Whether every edge of the solid is walked once each way by its faces.</summary>
    public static bool IsClosed(SolidMesh solid)
    {
        Dictionary<(int, int), int> edges = [];
        foreach (IReadOnlyList<int> face in solid.Faces)
        {
            for (int index = 0; index < face.Count; index++)
            {
                (int, int) edge = (face[index], face[(index + 1) % face.Count]);
                edges[edge] = edges.GetValueOrDefault(edge) + 1;
            }
        }

        foreach (((int from, int to), int count) in edges)
        {
            if (count != 1 || edges.GetValueOrDefault((to, from)) != 1)
            {
                return false;
            }
        }

        return true;
    }
}
