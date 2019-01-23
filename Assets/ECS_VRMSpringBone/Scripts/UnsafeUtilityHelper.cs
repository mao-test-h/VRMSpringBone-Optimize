namespace VRM.ECS_SpringBone
{
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe static class UnsafeUtilityHelper
    {
        public static void* Malloc<T>(Allocator allocator, int length = 1) where T : struct
        {
            var size = UnsafeUtility.SizeOf<T>() * length;
            void* ptr = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<T>(), allocator);
            UnsafeUtility.MemClear(ptr, size);
            return ptr;
        }
    }
}
