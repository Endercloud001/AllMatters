"""Minimal mathematical experiment; Python standard library; no game code."""
from collections import deque
from itertools import combinations
import json


def path(graph, start, target):
    queue = deque([[start]])
    seen = {start}
    while queue:
        route = queue.popleft()
        if route[-1] == target:
            return route
        for other in sorted(graph[route[-1]]):
            if other not in seen:
                seen.add(other)
                queue.append(route + [other])
    return None


def exact_walk(graph, normal, boss, budget):
    route = path(graph, 'N', boss)
    if route is None or len(route) - 1 > budget or len(normal) < budget - 1:
        return None
    entered = route[1:-1]
    seen = {'N', *entered}
    while len(entered) < budget - 1:
        options = sorted(v for v in normal - seen if graph[v] & seen)
        assert options
        entered.append(options[0])
        seen.add(options[0])
    assert graph[boss] & seen
    return entered + [boss]


def fixture(double):
    points = {'N': (0, 0)}
    groups = {'N': ['N']}
    groups['E'] = ['E0', 'E1', 'E2', 'E3']
    spacing = 4 if double else 3
    points.update({f'E{i}': (3 + i * spacing, 0) for i in range(4)})
    groups['W'] = [f'W{i}' for i in range(4)]
    points.update({f'W{i}': (-3 - i * 2, 0) for i in range(4)})
    for name, x in [('A', -11), ('C', -15)]:
        groups[name] = [f'{name}{i}' for i in range(4)]
        points.update({f'{name}0': (x, 0), f'{name}1': (x-2, 0),
                       f'{name}2': (x, 2), f'{name}3': (x-2, 2)})
    bosses = {'E2', 'E3'} if double else {'E3'}
    target = 'E2' if double else 'E3'
    normal = set(points) - bosses - {'N'}
    edges = [('N', 'E0'), ('N', 'W0'), ('W3', 'A0'), ('A1', 'C0')]
    external = set(edges)
    for group in ['E', 'W']:
        edges.extend((f'{group}{i}', f'{group}{i+1}') for i in range(3))
    for group in ['A', 'C']:
        edges.extend((f'{group}{i}', f'{group}{j}') for i,j in [(0,1),(0,2),(1,3),(2,3)])
    footprints = {}
    for name, (x,y) in points.items():
        radius = 1 if name in bosses | {'N'} else 0
        footprints[name] = {(a,b) for a in range(x-radius,x+radius+1)
                            for b in range(y-radius,y+radius+1)}
    for a,b in combinations(points,2):
        assert not footprints[a] & footprints[b]
    graph = {name: set() for name in points}
    occupied_paths = set()
    used_directions = {name: set() for name in points}
    for a,b in edges:
        x,y = points[a]
        u,v = points[b]
        assert x == u or y == v
        dx,dy = (u>x)-(u<x), (v>y)-(v<y)
        cells = {(x+dx*i, y+dy*i) for i in range(abs(x-u)+abs(y-v)+1)}
        interior = cells - footprints[a] - footprints[b]
        if (a,b) in external:
            assert len(interior) == 1
        assert not occupied_paths & interior
        for name in points:
            if name not in {a,b}:
                assert not footprints[name] & cells
        occupied_paths |= interior
        for name,direction in [(a,(dx,dy)), (b,(-dx,-dy))]:
            assert direction not in used_directions[name]
            used_directions[name].add(direction)
        graph[a].add(b)
        graph[b].add(a)
    for node in normal:
        reduced = {n: neighbors - bosses for n,neighbors in graph.items() if n not in bosses}
        assert path(reduced,'N',node)
    assert len(graph[target]) == (2 if double else 1)
    assert len(graph['E3']) == 1
    for budget in [15,14,13]:
        walk = exact_walk(graph,normal,target,budget)
        assert len(walk) == budget and len(set(walk)) == budget
        seen = {'N'}
        for node in walk:
            assert graph[node] & seen
            seen.add(node)
        print(json.dumps({'double_boss':double,'budget':budget,'normal_nodes':len(normal),
                          'shortest_boss':len(path(graph,'N',target))-1,'walk':walk}))


def independent_oracle(graph, normal, boss, budget):
    """Explore completed sets directly, without distance/count formula."""
    states = {frozenset({'N'})}
    for _ in range(budget - 1):
        states = {visited | {node} for visited in states for node in normal - visited
                  if graph[node] & visited}
    return any(graph[boss] & visited for visited in states)


def exhaustive_graph_check():
    checked = 0
    for n in range(1,5):
        vertices = ['N'] + [f'v{i}' for i in range(n)]
        pairs = list(combinations(vertices,2))
        for mask in range(1 << len(pairs)):
            graph = {v:set() for v in vertices}
            for i,(a,b) in enumerate(pairs):
                if mask >> i & 1:
                    graph[a].add(b)
                    graph[b].add(a)
            if not all(path(graph,'N',v) for v in vertices):
                continue
            for endpoint in vertices[1:]:
                extended = {v:set(edges) for v,edges in graph.items()}
                extended['B'] = {endpoint}
                extended[endpoint].add('B')
                for budget in range(1,n+3):
                    expected = n >= budget-1 and len(path(extended,'N','B'))-1 <= budget
                    oracle = independent_oracle(extended,set(vertices)-{'N'},'B',budget)
                    assert oracle == expected
                    result = exact_walk(extended,set(vertices)-{'N'},'B',budget)
                    assert (result is not None) == oracle
                    if result is not None:
                        assert len(result) == budget
                        visited = {'N'}
                        for node in result:
                            assert node not in visited and extended[node] & visited
                            visited.add(node)
                        assert result[-1] == 'B'
                    checked += 1
    print(json.dumps({'exhaustive_graph_budget_cases':checked,'failures':0}))


if __name__ == '__main__':
    fixture(False)
    fixture(True)
    exhaustive_graph_check()
