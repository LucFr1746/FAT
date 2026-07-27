namespace Domain.Entities;

/// <summary>
/// The binary content of a material, split out from <see cref="Material"/>.
/// One-to-one, sharing the same primary key.
/// </summary>
public class MaterialFile
{
    public int MaterialId { get; set; }

    public byte[] Content { get; set; } = [];

    public Material? Material { get; set; }
}
