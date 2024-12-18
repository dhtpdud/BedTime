using Unity.Entities;
using UnityEngine;

class MainSpawnerAuthoring : MonoBehaviour
{

}

class MainSpawnerAuthoringBaker : Baker<MainSpawnerAuthoring>
{
    public override void Bake(MainSpawnerAuthoring authoring)
    {
        AddComponent(GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic), new MainSpawnerTag());
    }
}
