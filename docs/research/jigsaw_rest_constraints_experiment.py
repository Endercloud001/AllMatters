"""Decision-only constructive experiment, Python standard library, no game code.

Run: python docs/research/jigsaw_rest_constraints_experiment.py
Checks one controlled 14-piece pool and four global quarter-turn orientations.
Does not claim arbitrary independent rotations preserve external connections.
"""
from collections import Counter
from itertools import combinations
import json

from jigsaw_exact_budget_experiment import path, exact_walk


def rotate(point, turns):
    x, y = point
    for _ in range(turns):
        x, y = -y, x
    return x, y


def run(double, turns):
    spacing = 4 if double else 3
    pieces = {
        'E': [(3+i*spacing, 0) for i in range(4)],
        'I': [(3, 2+2*i) for i in range(4)],
        'A': [(3,-2),(5,-2),(3,-4),(5,-4)],
        'C': [(7,-2),(9,-2),(7,-4),(9,-4)],
    }
    pool_shape = {'E':'I', 'I':'I', 'A':'O', 'C':'O'}
    skeletons = {
        'T': [(0,0),(1,0),(2,0),(1,1)],
        'S': [(1,0),(2,0),(0,1),(1,1)],
        'Z': [(0,0),(1,0),(1,1),(2,1)],
        'J': [(0,0),(0,1),(1,1),(2,1)],
        'L': [(2,0),(0,1),(1,1),(2,1)],
    }
    internal = {}
    for name in pieces:
        internal[name] = [(0,1),(1,2),(2,3)] if name in {'E','I'} else [(0,1),(0,2),(1,3),(2,3)]
    for shape, cells in skeletons.items():
        for copy in range(2):
            name = shape + str(copy)
            pool_shape[name] = shape
            internal[name] = [(i,j) for i,j in combinations(range(4),2)
                              if sum(abs(cells[i][k]-cells[j][k]) for k in (0,1)) == 1]
    assert Counter(pool_shape.values()) == Counter({s:2 for s in 'IOTSZJL'})
    room = {f'{name}{i}':'combat' for name in pool_shape for i in range(4)}
    rests = {'I0','I2','A0','A3','C0','C3'}
    for node in rests:
        room[node] = 'rest'
    candidates = []
    for shape,cells in skeletons.items():
        color = max((0,1),key=lambda c:sum((x+y)%2 == c for x,y in cells))
        side = [i for i,(x,y) in enumerate(cells) if (x+y)%2 == color][:2]
        for copy in range(2):
            candidates.extend(f'{shape}{copy}{i}' for i in side)
    for node,kind in zip(candidates[:8], ['shop']*4 + ['chest']*4):
        room[node] = kind
    free = [node for node in room if node[0] not in {'E','I','A','C'} and room[node] == 'combat']
    # 10 fixed combat in the four used pieces; reserve another 10 combat in leftovers.
    assert len(free) == 32
    for node,kind in zip(free, ['combat']*10 + ['question']*16 + ['elite']*6):
        room[node] = kind
    assert Counter(room.values()) == Counter(combat=20,question=16,elite=6,shop=4,rest=6,chest=4)
    for name,es in internal.items():
        for i,j in es:
            a,b = room[f'{name}{i}'],room[f'{name}{j}']
            assert not (a == b and a in {'rest','shop','chest'})
    bosses = {'E2','E3'} if double else {'E3'}
    target = 'E2' if double else 'E3'
    for node in bosses:
        assert room[node] in {'combat','question','elite'}
        room[node] = 'boss'
    points = {'N':(0,0)}
    points.update({f'{name}{i}':rotate(point,turns) for name,cells in pieces.items() for i,point in enumerate(cells)})
    owner = {node:name for name in pieces for node in (f'{name}{i}' for i in range(4))}
    owner['N'] = 'N'
    external = [('N','E0'),('E0','I0'),('E0','A0'),('A1','C0')]
    edges = external + [(f'{name}{i}',f'{name}{j}') for name in pieces for i,j in internal[name]]
    footprints = {}
    for node,(x,y) in points.items():
        radius = int(node in bosses | {'N'})
        footprints[node] = {(u,v) for u in range(x-radius,x+radius+1) for v in range(y-radius,y+radius+1)}
    for a,b in combinations(points,2):
        assert not footprints[a] & footprints[b]
    graph = {node:set() for node in points}
    path_cells = set()
    ports = {node:set() for node in points}
    for a,b in edges:
        x,y = points[a]
        u,v = points[b]
        assert x == u or y == v
        dx,dy = (u>x)-(u<x),(v>y)-(v<y)
        cells = {(x+i*dx,y+i*dy) for i in range(abs(x-u)+abs(y-v)+1)}
        interior = cells - footprints[a] - footprints[b]
        if (a,b) in external:
            assert len(interior) == 1
            assert a not in bosses and b not in bosses
        assert not path_cells & interior
        for node in points.keys() - {a,b}:
            assert not cells & footprints[node]
        path_cells |= interior
        for node,direction in [(a,(dx,dy)),(b,(-dx,-dy))]:
            assert direction not in ports[node]
            ports[node].add(direction)
        graph[a].add(b)
        graph[b].add(a)
    normal = set(points) - bosses - {'N'}
    reduced = {node:neighbors-bosses for node,neighbors in graph.items() if node not in bosses}
    assert all(path(reduced,'N',node) for node in normal)
    assert len(graph[target]) == (2 if double else 1)
    assert len(graph['E3']) == 1
    used = set(pieces)
    rest_pieces = {name for name in pool_shape if any(room[f'{name}{i}'] == 'rest' for i in range(4))}
    assert rest_pieces == {'I','A','C'} and rest_pieces <= used
    boss_neighbors = {owner[b] if owner[a] == 'E' else owner[a]
                      for a,b in external if 'E' in {owner[a],owner[b]} and 'N' not in {owner[a],owner[b]}}
    assert boss_neighbors == {'I','A'}
    pairs = Counter(tuple(sorted((owner[a],owner[b]))) for a,b in external if 'N' not in {owner[a],owner[b]})
    assert all(count <= 2 for count in pairs.values())
    for budget in [15,14,13]:
        walk = exact_walk(graph,normal,target,budget)
        assert walk and len(walk) == budget
        visited = {'N'}
        for node in walk:
            assert node not in visited and graph[node] & visited
            visited.add(node)
        assert walk[-1] == target
    print(json.dumps({'double_boss':double,'quarter_turns':turns,'pool_pieces':len(pool_shape),
                      'normal_nodes_used':len(normal),'d':len(path(graph,'N',target))-1,
                      'rest_nodes':len(rests),'rest_pieces_used':sorted(rest_pieces),
                      'boss_piece_other_neighbors':sorted(boss_neighbors),'budgets_passed':[15,14,13]}))


if __name__ == '__main__':
    for double in [False,True]:
        for turns in range(4):
            run(double,turns)
