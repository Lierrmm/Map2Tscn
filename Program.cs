using System.Numerics;

namespace Map2Tscn;

public abstract class Program
{
    private const string MapPath = @"G:\SteamLibrary\steamapps\common\Call of Duty 4\iw3xo\map_export\mp_shipment.map";

    private static void Main()
    {
        var brushes = ParseFile(MapPath);

        GenerateResources(brushes, @"F:\game_dev\ares\arenas");

        Console.WriteLine("TSCN file generated successfully.");
    }

    private static List<Brush> ParseFile(string filePath)
    {
        var brushes = new List<Brush>();
        var lines = File.ReadAllLines(filePath);
        
        Brush? currentBrush = null;
        var inMesh = false;
        var inVertexBlock = false;
        var currentMeshVertices = new List<Vertex>();

        foreach (var t in lines)
        {
            var line = t.Trim();

            if (line.StartsWith("// brush"))
            {
                currentBrush = new Brush
                {
                    Id = int.Parse(line.Split(' ')[2])
                };
                brushes.Add(currentBrush);
                continue;
            }

            if (line.StartsWith("layer"))
            {
                if (currentBrush != null) currentBrush.Layer = line.Split('"')[1];
                continue;
            }

            if (line.StartsWith("mesh"))
            {
                if (currentBrush != null) currentBrush.IsMesh = true;
                inMesh = true;
                continue;
            }

            if (inMesh && line.StartsWith("("))
            {
                inVertexBlock = true;
                continue;
            }

            if (inVertexBlock && line.StartsWith("v "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var vertex = new Vertex
                {
                    X = float.Parse(parts[1]),
                    Y = float.Parse(parts[2]),
                    Z = float.Parse(parts[3])
                };
                currentMeshVertices.Add(vertex);
                
                if (currentMeshVertices.Count == 2)
                {
                    currentBrush?.MeshLines.Add((currentMeshVertices[0], currentMeshVertices[1]));
                    currentMeshVertices.Clear();
                }
                continue;
            }

            if (line.StartsWith("(") && !inMesh)
            {
                // Regular brush face format
                var parts = line.Split(')')[0] + ")"; // Get just the vertex definition part
                var coordSets = parts.Split(['('], StringSplitOptions.RemoveEmptyEntries);
                
                // Get material name (if needed)
                var remainingParts = line.Split(')')[1].Trim().Split(' ');
                
                var face = new Face(remainingParts[0]);
                foreach (var coords in coordSets)
                {
                    if (!string.IsNullOrWhiteSpace(coords))
                    {
                        face.Vertices.Add(Vertex.Parse(coords));
                    }
                }

                

                currentBrush?.Faces.Add(face);
                continue;
            }

            if (line != "}") continue;
            inMesh = false;
            inVertexBlock = false;
            currentMeshVertices.Clear();
        }

        return brushes;
    }

    private static void GenerateResources(List<Brush> brushes, string path)
    {
        // Generate .tscn file
        var tscnPath = Path.Combine(path, "mp_shipment.tscn");
        var extResource1 = $"1_{GenerateUid()}";
        var extResource2 = $"2_{GenerateUid()}";
        var extResource3 = $"3_{GenerateUid()}";
        var extResource4 = $"4_{GenerateUid()}";
        var extResource5 = $"5_{GenerateUid()}";
        
        using var writer = new StreamWriter(tscnPath);
        // Header
        writer.WriteLine("[gd_scene load_steps={0} format=3 uid=\"uid://mp_shipment\"]", brushes.Count + 1);
        writer.WriteLine();
        writer.WriteLine("[ext_resource type=\"Script\" path=\"res://core/Arena/Arena.cs\" id=\"1_lokqm\"]\n");
        writer.WriteLine($"[ext_resource type=\"Script\" path=\"res://addons/cyclops_level_builder/nodes/cyclops_block.gd\" id=\"{extResource2}\"]");
        writer.WriteLine($"[ext_resource type=\"Script\" path=\"res://addons/cyclops_level_builder/resources/mesh_vector_data.gd\" id=\"{extResource1}\"]");
        writer.WriteLine($"[ext_resource type=\"Script\" path=\"res://addons/cyclops_level_builder/resources/data_vector_float.gd\" id=\"{extResource3}\"]");
        writer.WriteLine($"[ext_resource type=\"Script\" path=\"res://addons/cyclops_level_builder/resources/data_vector_byte.gd\" id=\"{extResource4}\"]");
        writer.WriteLine($"[ext_resource type=\"Script\" path=\"res://addons/cyclops_level_builder/resources/data_vector_int.gd\" id=\"{extResource5}\"]");
        writer.WriteLine();
        
        foreach (var brush in brushes)
        {
            WriteCyclopsResource(writer, brush, extResource1, extResource3, extResource4, extResource5);
        }
        
        
        // Root node
        writer.WriteLine("[node name=\"Map\" type=\"Node3D\"]");
        writer.WriteLine($"script = ExtResource(\"1_lokqm\")");
        
        foreach (var brush in brushes.Where(brush => !brush.IsMesh))
        {
            WriteCyclopsBrush(writer, brush, extResource2);
        }
    }

    private static string GenerateUid()
    {
        return Guid.NewGuid().ToString("N")[..6];
    }
    
    private static Vector3 Normalize(Vector3 vec)
    {
        var length = vec.Length();
        return length == 0 ? new Vector3(0, 0, 0) : // Avoid division by zero for zero-length vectors
            new Vector3(vec.X / length, vec.Y / length, vec.Z / length);
    }
    
    private static Vector3[]? CalculateFaceNormals(List<Vertex> vertexPositions, List<int> faceVertexIndices, int numVertices)
    {
        var faceNormals = Array.Empty<Vector3>();

        // Loop through face indices (assuming triangles for simplicity)
        for (var i = 0; i < faceVertexIndices.Count; i += 3)
        {
            if (i + 2 >= faceVertexIndices.Count)
            {
                // Skip this face if there aren't enough indices for a full triangle
                Console.WriteLine("Skipping incomplete face (not enough indices for a triangle).");
                break;
            }
            
            // Get vertex indices for the face
            var i1 = faceVertexIndices[i];
            var i2 = faceVertexIndices[i + 1];
            var i3 = faceVertexIndices[i + 2];

            if (i1 < 0 || i1 >= numVertices || i2 < 0 || i2 >= numVertices || i3 < 0 || i3 >= numVertices) continue;
            // Fetch vertex positions
            var v1 = new Vector3(vertexPositions[i1].X, vertexPositions[i1].Y, vertexPositions[i1].Z);
            var v2 = new Vector3(vertexPositions[i2].X, vertexPositions[i2].Y, vertexPositions[i2].Z);
            var v3 = new Vector3(vertexPositions[i3].X, vertexPositions[i3].Y, vertexPositions[i3].Z);

            // Calculate edges
            var edge1 = v2 - v1;
            var edge2 = v3 - v1;

            // Compute the normal
            var normal = Vector3.Cross(edge1, edge2);
            normal = Normalize(normal);
            // Add the normal to the array
            faceNormals?.Append(normal);
        }

        return faceNormals;
    }

    private static void WriteCyclopsResource(StreamWriter writer, Brush brush, string extResource, string vResource, string sResource, string iResource)
    {
        if (brush.IsMesh) return;
        // Collect all unique vertices and build indices
        var uniqueVertices = new List<Vertex>();
        var edgeVertexIndices = new List<int>();
        var faceVertexIndices = new List<int>();
        var faceVertexCounts = new List<int>();

        if (brush.IsMesh)
        {
            foreach (var line in brush.MeshLines)
            {
                AddUniqueVertex(uniqueVertices, line.v1, out int v1Index);
                AddUniqueVertex(uniqueVertices, line.v2, out int v2Index);
                edgeVertexIndices.Add(v1Index);
                edgeVertexIndices.Add(v2Index);
            }
        }
        else
        {
            foreach (var face in brush.Faces)
            {
                faceVertexCounts.Add(face.Vertices.Count);
                
                for (var i = 0; i < face.Vertices.Count; i++)
                {
                    AddUniqueVertex(uniqueVertices, face.Vertices[i], out int vIndex);
                    faceVertexIndices.Add(vIndex);
                    
                    var nextIndex = (i + 1) % face.Vertices.Count;
                    AddUniqueVertex(uniqueVertices, face.Vertices[nextIndex], out int nextVIndex);
                    edgeVertexIndices.Add(vIndex);
                    edgeVertexIndices.Add(nextVIndex);
                }
            }
        }
        
        // Vertex Resource
        var vertexName = $"Resource_v{GenerateUid()}";
        writer.WriteLine($"[sub_resource type=\"Resource\" id=\"{vertexName}\"]");
        writer.WriteLine($"script = ExtResource(\"{vResource}\")");
        writer.Write("data = PackedFloat32Array(");
        for (var i = 0; i < uniqueVertices.Count; i++)
        {
            writer.Write($"{uniqueVertices[i].X:F1}, {uniqueVertices[i].Y:F1}, {uniqueVertices[i].Z:F1}");

            if (i < uniqueVertices.Count - 1)
                writer.Write(", ");
        }
        writer.WriteLine(")");
        writer.WriteLine("name = &\"position\"");
        writer.WriteLine("category = \"\"");
        writer.WriteLine("data_type = 6");
        writer.WriteLine("stride = 3");
        writer.WriteLine();
        
        var vertexSelectedName = $"Resource_v{GenerateUid()}";
        writer.WriteLine($"[sub_resource type=\"Resource\" id=\"{vertexSelectedName}\"]");
        writer.WriteLine($"script = ExtResource(\"{sResource}\")");
        writer.Write("data = PackedByteArray(");
        for (var i = 0; i < uniqueVertices.Count; i++)
        {
            writer.Write("0");
            if (i < uniqueVertices.Count - 1) writer.Write(", ");
        }
        writer.WriteLine(")");
        writer.WriteLine("name = &\"selected\"");
        writer.WriteLine("category = \"\"");
        writer.WriteLine("data_type = 0");
        writer.WriteLine("stride = 1");
        
        writer.WriteLine($"\n[sub_resource type=\"Resource\" id=\"edge_{brush.Id}\"]");
        writer.WriteLine($"script = ExtResource(\"{sResource}\")");
        writer.Write("data = PackedByteArray(");
        for (var i = 0; i < edgeVertexIndices.Count; i++)
        {
            writer.Write("0");
            if (i < edgeVertexIndices.Count - 1) writer.Write(", ");
        }
        writer.WriteLine(")");
        writer.WriteLine("name = &\"selected\"");
        writer.WriteLine("category = \"\"");
        writer.WriteLine("data_type = 0");
        writer.WriteLine("stride = 1");
        
        writer.WriteLine($"\n[sub_resource type=\"Resource\" id=\"material_{brush.Id}\"]");
        writer.WriteLine($"script = ExtResource(\"{iResource}\")");
        writer.WriteLine("data = PackedInt32Array(1, 1, 1, 1, 1, 1)");
        writer.WriteLine("name = &\"material_index\"");
        writer.WriteLine("category = \"\"");
        writer.WriteLine("data_type = 1");
        writer.WriteLine("stride = 1");
        
        writer.WriteLine($"\n[sub_resource type=\"Resource\" id=\"uv_{brush.Id}\"]");
        writer.WriteLine($"script = ExtResource(\"{vResource}\")");
        writer.Write("data = PackedFloat32Array(");
        for (var i = 0; i < brush.Faces.Count; i++)
        {
            writer.Write("1, 0, 0, 1, 0, 0");
            if (i < brush.Faces.Count - 1) writer.Write(", ");
        }
        writer.WriteLine(")");
        writer.WriteLine("name = &\"uv_transform\"");
        writer.WriteLine("category = \"\"");
        writer.WriteLine("data_type = 8");
        writer.WriteLine("stride = 6");
        
        writer.WriteLine($"\n[sub_resource type=\"Resource\" id=\"color_{brush.Id}\"]");
        writer.WriteLine($"script = ExtResource(\"{vResource}\")");
        writer.Write("data = PackedFloat32Array(");
        for (var i = 0; i < brush.Faces.Count; i++)
        {
            writer.Write("1");
            if (i < brush.Faces.Count - 1) writer.Write(", ");
        }
        writer.WriteLine(")");
        writer.WriteLine("name = &\"color\"");
        writer.WriteLine("category = \"\"");
        writer.WriteLine("data_type = 4");
        writer.WriteLine("stride = 4");
        
        writer.WriteLine($"\n[sub_resource type=\"Resource\" id=\"face_index_{brush.Id}\"]");
        writer.WriteLine($"script = ExtResource(\"{iResource}\")");
        writer.WriteLine("data = PackedInt32Array(");
        for (var i = 0; i < brush.Faces.Count - 1; i++)
        {
            writer.Write($"{i}, {i}, {i}, {i}");
            if (i < brush.Faces.Count - 2) writer.Write(", ");
        }
        writer.WriteLine(")");
        writer.WriteLine("name = &\"face_index\"");
        writer.WriteLine("category = \"\"");
        writer.WriteLine("data_type = 1");
        writer.WriteLine("stride = 1");
        
        // [sub_resource type="Resource" id="Resource_rpkgu"]
        // script = ExtResource("4_8jx8v")
        // data = PackedFloat32Array() num_edges * num_faces
        // name = &"normal"
        // category = ""
        // data_type = 6
        // stride = 3
        
        var normals = CalculateFaceNormals(uniqueVertices, faceVertexIndices, uniqueVertices.Count);
        
        writer.WriteLine($"\n[sub_resource type=\"Resource\" id=\"normal_{brush.Id}\"]");
        writer.WriteLine($"script = ExtResource(\"{vResource}\")");
        writer.Write("data = PackedFloat32Array(");

        
        for (var i = 0; i < normals?.Length; i++)
        {
            writer.Write($"{normals[i].X:F1}, {normals[i].Y:F1}, {normals[i].Z:F1}");
            if (i < normals.Length - 1) writer.Write(", ");
        }
        
        writer.WriteLine(")");
        writer.WriteLine("name = &\"normal\"");
        writer.WriteLine("category = \"\"");
        writer.WriteLine("data_type = 6");
        writer.WriteLine("stride = 3");
        
        // [sub_resource type="Resource" id="Resource_3o3uu"]
        // script = ExtResource("3_vp8mo")
        // data = PackedInt32Array(0, 1, 2, 3, 7, 6, 5, 4, 1, 0, 4, 5, 2, 1, 5, 6, 3, 2, 6, 7, 0, 3, 7, 4)
        // name = &"vertex_index"
        // category = ""
        // data_type = 1
        // stride = 1
            
        writer.WriteLine($"\n[sub_resource type=\"Resource\" id=\"vertex_index_{brush.Id}\"]");
        writer.WriteLine($"script = ExtResource(\"{iResource}\")");
        writer.Write("data = PackedInt32Array(");
        for (var i = 0; i < faceVertexIndices.Count; i++)
        {
            writer.Write(faceVertexIndices[i]);
            if (i < faceVertexIndices.Count - 1) writer.Write(", ");
        }
        writer.WriteLine(")");
        writer.WriteLine("name = &\"vertex_index\"");
        writer.WriteLine("category = \"\"");
        writer.WriteLine("data_type = 1");
        writer.WriteLine("stride = 1");
        
        // Block Resource
        var resourceName = $"Resource_Block{brush.Id}";
        writer.WriteLine($"\n[sub_resource type=\"Resource\" id=\"{resourceName}\"]");
        writer.WriteLine($"script = ExtResource(\"{extResource}\")");

        // Write counts first
        writer.WriteLine($"num_vertices = {uniqueVertices.Count}");
        writer.WriteLine($"num_edges = {edgeVertexIndices.Count / 2}");
        writer.WriteLine($"num_faces = {(brush.IsMesh ? 0 : brush.Faces.Count)}");
        writer.WriteLine($"num_face_vertices = {faceVertexIndices.Count}");
        
        // Write active states
        writer.WriteLine("active_vertex = -1");
        writer.WriteLine("active_edge = -1");
        writer.WriteLine("active_face = -1");
        writer.WriteLine("active_face_vertex = -1");

        // Write edge vertex indices
        writer.Write("edge_vertex_indices = PackedInt32Array(");
        for (var i = 0; i < edgeVertexIndices.Count; i++)
        {
            writer.Write(edgeVertexIndices[i]);
            if (i < edgeVertexIndices.Count - 1) writer.Write(", ");
        }
        writer.WriteLine(")");

        if (!brush.IsMesh)
        {
            // Write face vertex counts
            writer.Write("face_vertex_count = PackedInt32Array(");
            for (var i = 0; i < faceVertexCounts.Count; i++)
            {
                writer.Write(faceVertexCounts[i]);
                if (i < faceVertexCounts.Count - 1) writer.Write(", ");
            }
            writer.WriteLine(")");

            // Write face vertex indices
            writer.Write("face_vertex_indices = PackedInt32Array(");
            for (var i = 0; i < faceVertexIndices.Count; i++)
            {
                writer.Write(faceVertexIndices[i]);
                if (i < faceVertexIndices.Count - 1) writer.Write(", ");
            }
            writer.WriteLine(")");
            
            // Write edge data dictionary
            writer.WriteLine("edge_data = {");
            writer.WriteLine($"\"selected\": SubResource(\"edge_{brush.Id}\")");
            writer.WriteLine("}");

            // Write face data dictionary
            writer.WriteLine("face_data = {");
            writer.WriteLine($"\"selected\": SubResource(\"{vertexSelectedName}\"),");
            writer.WriteLine($"\"visible\": SubResource(\"{vertexSelectedName}\"),");
            writer.WriteLine($"\"material_index\": SubResource(\"material_{brush.Id}\"),");
            writer.WriteLine($"\"uv_transform\": SubResource(\"uv_{brush.Id}\"),");
            writer.WriteLine($"\"color\": SubResource(\"color_{brush.Id}\")");
            writer.WriteLine("}");
            
            // face_vertex_data = {
            //     "color": SubResource("Resource_jfkdh"),
            //     "face_index": SubResource("Resource_bjlco"),
            //     "normal": SubResource("Resource_rpkgu"),
            //     "vertex_index": SubResource("Resource_3o3uu")
            // }
            
            writer.WriteLine("face_vertex_data = {");
            writer.WriteLine($"\"vertex_index\": SubResource(\"vertex_index_{brush.Id}\"),");
            writer.WriteLine($"\"normal\": SubResource(\"normal_{brush.Id}\"),");
            writer.WriteLine($"\"face_index\": SubResource(\"face_index_{brush.Id}\"),");
            writer.WriteLine($"\"color\": SubResource(\"color_{brush.Id}\")");
            writer.WriteLine("}");
        }
        
        writer.WriteLine("vertex_data = {");
        writer.WriteLine($"\"position\": SubResource(\"{vertexName}\"),");
        writer.WriteLine($"\"selected\": SubResource(\"{vertexSelectedName}\")");
        writer.WriteLine("}");
        
        writer.WriteLine();
    }
    
    private static void WriteCyclopsBrush(StreamWriter writer, Brush brush, string extResource)
    {
        var resourceName = $"Resource_Block{brush.Id}";
       
        writer.WriteLine($"\n[node name=\"Block_{brush.Id}\" type=\"Node3D\" parent=\".\"]");
        // writer.WriteLine("transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, -54, 0, -22)");
        writer.WriteLine($"script = ExtResource(\"{extResource}\")");
        writer.WriteLine($"mesh_vector_data = SubResource(\"{resourceName}\")");
    }
    
    private static void AddUniqueVertex(List<Vertex> vertices, Vertex vertex, out int index)
    {
        index = vertices.FindIndex(v => 
            Math.Abs(v.X - vertex.X) < 0.001f && 
            Math.Abs(v.Y - vertex.Y) < 0.001f && 
            Math.Abs(v.Z - vertex.Z) < 0.001f);

        if (index != -1) return;
        index = vertices.Count;
        vertices.Add(vertex);
    }
}

public class Vertex
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    public static Vertex Parse(string coords)
    {
        var parts = coords.Trim('(', ')', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new Vertex
        {
            X = float.Parse(parts[0]),
            Y = float.Parse(parts[1]),
            Z = float.Parse(parts[2])
        };
    }
}

public class Face(string material)
{
    public List<Vertex> Vertices { get; set; } = [];
    public string Material { get; set; } = material;
}

public class Brush
{
    public int Id { get; init; }
    public string? Layer { get; set; }
    public List<Face> Faces { get; set; } = [];
    public bool IsMesh { get; set; }
    public List<(Vertex v1, Vertex v2)> MeshLines { get; set; } = [];
}