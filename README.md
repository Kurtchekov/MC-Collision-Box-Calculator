#MC Collision Box Calculator

Steps:
1. Download and install the latest version of Unity.
2. Create a new project and paste the contents of this repository's Assets folder into your project's.
3. Under Window menu, select "AABB".
4. Import your model into Unity. Use OBJ files, even if Unity accepts other formats! Make sure your model doesn't have any polygons hidden inside geometry, otherwise it might cause miscalculation of your AABBs.
5. Place your model in the scene, set coordinates to 0, 0, 0. Not doing so won't affect the end result, but you might notice a mismatch between the drawn AABBs and your model.
6. Configure, double check settings and, once ready, hit "Calculate All". This is a multithreaded process and won't freeze your UI while doing its thing. Give it some time.
7. Once it's over, use the "Export" option.


This code will generate a long list of all blocks, one block position per line, each AABB contained within curly brackets. Null means an empty block, empty brackets means full block. Values range from 0 to 16, so that you can use bytes rather than floats or doubles to represent AABBs as data.