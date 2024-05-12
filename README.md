# NavMeshDynamic

NavMeshDynamic Tool (NMD Tool) is a real-time NavMesh Generator that operates without the need for preprocessing, with good performance, and utilizing parallel processing capabilities. NMD is designed to generate NavMesh for real-time generated maps, fitting rogue-like games and infinitely expanding maps.

## How It Works?

NMD distributes the work needed to generate NavMesh (such as reading meshes, merging vertices by distance, etc.) across chunks and frames. It only performs work within a user-defined radius around the agent. As the agent continues to move, more chunks come within range. When a chunk enters the radius, the system places the necessary work in a queue.

## Performance

The NavMeshDynamic Tool integrates the Job system and Burst compiler for enhanced performance. Leveraging these technologies, the tool efficiently distributes tasks across threads, maximizing parallel processing capabilities. Additionally, optimizations will focus on reducing memory overhead and improving computational efficiency, ensuring smooth operation even in resource-intensive scenarios. Overall, these enhancements aim to elevate the tool's performance, making it well-suited for real-time generation of NavMesh in dynamic and expansive game environments.



## License
Shield: [![CC BY-ND 4.0][cc-by-nd-shield]][cc-by-nd]

This work is licensed under a
[Creative Commons Attribution-NoDerivs 4.0 International License][cc-by-nd].

[![CC BY-ND 4.0][cc-by-nd-image]][cc-by-nd]

[cc-by-nd]: https://creativecommons.org/licenses/by-nd/4.0/

[cc-by-nd-image]: https://licensebuttons.net/l/by-nd/4.0/88x31.png
[cc-by-nd-shield]: https://img.shields.io/badge/License-CC%20BY--ND%204.0-lightgrey.svg
