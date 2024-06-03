# Navigation Mesh Dynamic Tool (NMD Tool)

## Project Overview
The Navigation Mesh Dynamic Tool (NMD Tool) is a 3D navigation tool for game AI’s which generates navigation meshes in real-time without causing performance issues. This project runs on the Unity Game Engine and aims to provide seamless navigation for AI agents within complex and dynamically expanding game environments.

## Team Members
- Kerem Aydoğan (Student ID: 21945829)
- Kübra Özdamar (Student ID: 21946481)
- Supervisor: Ali Seydi Keçeli

## Problem Definition
In modern game development, the effective navigation of AI agents within 3D environments is critical for creating immersive and engaging gameplay. Traditional navigation meshes are static, becoming quickly outdated in ever-changing game worlds. The primary challenge addressed by this project is the generation of navigation meshes in real-time for expanding game worlds. Static navigation meshes are precomputed during the game design phase, making them unsuitable for environments where the map can change frequently. This project aims to overcome these limitations by enabling the real-time generation and adaptation of navigation meshes in procedurally expanding game environments.

## Introduction
In the realm of game development, effective navigation of AI agents within 3D environments is paramount for creating immersive and engaging gameplay. Traditional methods like static navigation meshes are limited in their ability to handle expanding, procedurally generated game worlds. This project, the Navigation Mesh Dynamic Tool (NMD Tool), aims to overcome these limitations by enabling the real-time generation of navigation meshes in procedurally expanding game environments. This tool is essential for modern games where the environment grows and evolves continuously, providing a more flexible and responsive navigation system for AI agents.

## Literature Review
### Static Navigation Meshes (Traditional Approach)
Traditional navigation meshes are precomputed structures defining walkable areas in a game environment. They are created during the game design phase and do not adapt to changes in the game world. Works such as "AI Game Programming Wisdom" by Steve Rabin and "Polygonal Representation of Navigation Meshes" by David M. Bourguignon highlight the use of static navigation meshes in game development. These methods are effective in static environments but require complete recompilation if any changes occur, making them impractical for expanding game worlds.

### Hierarchical Pathfinding A*
Hierarchical Pathfinding A* (HPA*) is a method that reduces the complexity of pathfinding by dividing the environment into regions and performing pathfinding at multiple levels of abstraction. Botea, Muller, and Schaeffer's work on "Near Optimal Hierarchical Pathfinding" demonstrates the efficiency of HPA* in large environments by simplifying the search space. This approach is beneficial for managing large datasets and reducing computational load, which is critical for real-time navigation in expanding game environments. Our project uses a version of HPA* for critical pathfinding tasks, ensuring efficient navigation through newly generated areas.

### Dynamic Obstacle Avoidance
While dynamic obstacle avoidance focuses on adjusting AI paths to avoid moving obstacles, it is not within the scope of our project. However, understanding its principles is important for potential future enhancements. The work by van den Berg, Lin, and Manocha in "Reciprocal Velocity Obstacles for Real-Time Multi-Agent Navigation" provides insights into real-time obstacle avoidance, which could be integrated into future versions of our tool to enhance pathfinding in dynamic environments.

### Job System and Burst Compiler
Advancements in parallel computing, such as Unity's C# Job System and Burst Compiler, have revolutionized the way real-time computation is handled in game development. "Structured Parallel Programming" by McCool, Reinders, and Robison outlines foundational parallel computing patterns essential for implementing efficient real-time systems. Unity's Job System allows for the efficient distribution of computational tasks across multiple cores, while the Burst Compiler optimizes these tasks for performance. These technologies are crucial for our project, enabling the real-time generation of navigation meshes without significant performance overhead.

## Proposed Solution
Our hypothesis is that real-time navigation mesh generation can be achieved by integrating advanced parallel computing techniques, such as Unity's C# Job System and Burst Compiler, with hierarchical pathfinding algorithms. The NMD Tool aims to dynamically generate navigation meshes during gameplay, allowing AI agents to navigate efficiently in procedurally expanding game worlds.

### Methodology
The methodology for developing the NMD Tool involves a systematic approach to each phase of the project. Initially, data ingestion and preprocessing ensure that all necessary game environment data is collected and optimized for efficient processing. Dynamic map adaptation algorithms handle real-time changes in the environment, generating navigation meshes as new areas are added.

The core of the tool, navigation mesh generation, uses a hierarchical leveled area system to manage and process data efficiently. This system allows for the structured generation of nodes and their organization into a navigation mesh. Parallel programming techniques, implemented through Unity's Job System and Burst Compiler, ensure that these computationally intensive tasks are performed efficiently, leveraging multiple cores for optimal performance.

The final component, the pathfinding algorithm, integrates the A* algorithm to provide intelligent pathfinding solutions based on the updated navigation mesh. This enables AI agents to navigate procedurally expanding environments effectively.

### Results and Discussion
The implementation of the NMD Tool demonstrates the feasibility of real-time navigation mesh generation. By leveraging parallel computing techniques and hierarchical pathfinding algorithms, the tool successfully generates, and updates navigation meshes during gameplay. Initial testing indicates significant improvements in AI navigation efficiency and responsiveness, with the tool effectively handling expansions in the game environment. However, the tool currently does not support updating the same areas dynamically, which remains an area for future improvement.

### Expected Impact and Future Directions
The NMD Tool is expected to significantly enhance the gaming experience by providing more immersive and responsive AI navigation. Game developers can create more complex and procedurally expanding game worlds without the limitations of static navigation meshes. Future directions for this project include further optimization of the tool, integration with broader game development pipelines, and exploration of advanced pathfinding strategies, such as dynamic obstacle avoidance and real-time updates to existing areas of the navigation mesh.

In conclusion, the NMD Tool addresses a critical need in game development by enabling real-time navigation mesh generation for expanding, procedurally generated maps. Through the integration of advanced parallel computing techniques and hierarchical pathfinding algorithms, the tool offers a robust solution that enhances AI navigation in dynamic 3D environments. This project not only contributes to the field of game development but also provides a foundation for future research and innovation in real-time AI navigation systems.

## Technical Implementation Details
### Datasets
In our project, the primary dataset comprises 3D mesh data from the game environment, including terrain, obstacles, and other structural elements necessary for AI navigation. This data is essential for generating navigation meshes in real-time for procedurally expanding maps.

### Data Extraction
The raw data for the navigation mesh generation consists of vertices and indices extracted from the 3D meshes within the game environment. Each mesh is associated with a specific layer that determines its relevance to the navigation system. The extraction process involves collecting these vertices and indices, which serve as the foundation for building the navigation mesh.

### Preprocessing Steps
- **Elimination of Invalid Triangles**: Before utilizing the mesh data, it is crucial to filter out invalid triangles that do not contribute to the walkable surface. Triangles with a slope exceeding a predefined threshold are considered invalid. This is achieved through the following steps:
  - **Standard Method**: Iterate through each set of three indices (representing a triangle) in the mesh. Calculate the normal of the triangle using the vertices at these indices. Compare the slope of the triangle against the maximum allowable slope. Retain only those triangles whose slopes are within the acceptable range.
  - **Parallelized Method Using Jobs**: Utilize Unity’s Job System and Burst Compiler to perform the same task in parallel, significantly improving performance. Create and schedule jobs to process multiple triangles simultaneously. Dispose of native arrays after the job completion to free memory.

- **Vertex Arrangement**: To facilitate efficient vertex merging, the vertices are arranged according to a rounding number. This process ensures that vertices close to each other are rounded to the same position, making it easier to identify and merge them later.
  - **Standard Method**: Transform each vertex from local space to world space. Round the transformed vertex positions to a specified precision (vertex merge threshold). Update the vertex array with the rounded positions.
  - **Parallelized Method Using Jobs**: Utilize Unity’s Job System to parallelize the vertex arrangement process. Schedule jobs to handle large arrays of vertices concurrently, improving the efficiency of the preprocessing step.

### Adding Vertices to Chunk Lists
After preprocessing, the vertices are added to chunk lists, which are data structures designed to organize vertices into manageable segments (chunks). This step involves:
1. Iterating through the array of vertices.
2. Adding each vertex to the appropriate chunk list based on its position.
3. Keeping track of the vertices' chunk list indices (CLI) for later use in mesh generation and merging operations.

### Work System for Task Scheduling
- **Description**: Manages the execution of tasks in a priority queue, ensuring that the system operates within defined time limits to prevent frame drops.
- **Usage in Project**: Ensures that the NMD Tool operates without causing performance issues, allowing the game to run smoothly.

### Leveled Area System
- **Description**: Organizes the environment into hierarchical levels of chunks, each containing different levels of detail. This system helps manage dependencies between various computational tasks and supports efficient data processing.
- **Usage in Project**: Facilitates structured navigation mesh generation, ensuring that higher-level tasks are completed before lower-level tasks.

### Hierarchical Pathfinding A*
- **Description**: Utilizes a hierarchical approach to pathfinding by dividing the environment into regions and performing pathfinding at multiple levels of abstraction. This reduces the complexity of pathfinding in large environments.
- **Usage in Project**: Applied for critical pathfinding tasks to ensure efficient


## License
Shield: [![CC BY-ND 4.0][cc-by-nd-shield]][cc-by-nd]

This work is licensed under a
[Creative Commons Attribution-NoDerivs 4.0 International License][cc-by-nd].

[![CC BY-ND 4.0][cc-by-nd-image]][cc-by-nd]

[cc-by-nd]: https://creativecommons.org/licenses/by-nd/4.0/

[cc-by-nd-image]: https://licensebuttons.net/l/by-nd/4.0/88x31.png
[cc-by-nd-shield]: https://img.shields.io/badge/License-CC%20BY--ND%204.0-lightgrey.svg
