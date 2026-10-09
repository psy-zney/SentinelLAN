'use strict';

// GHSA-vfj7-8cjw-p6xm: bound the recursive walkers, including caller-supplied ASTs.
module.exports = function assertDepth(ast) {
  const pending = [{ node: ast, depth: 0 }];
  let nodes = 0;
  while (pending.length) {
    const { node, depth } = pending.pop();
    if (depth > 100 || ++nodes > 20000) {
      throw new SyntaxError('Brace expression exceeds the safe nesting limit');
    }
    if (node && Array.isArray(node.nodes)) {
      for (const child of node.nodes) {
        pending.push({ node: child, depth: depth + 1 });
      }
    }
  }
};
