import { VideoChunkDto } from './violation.service';

export function findEvidenceChunk(chunks: VideoChunkDto[], seconds: number): VideoChunkDto | undefined {
  return chunks.find(c => {
    const start = c.metadata?.startOffsetSeconds;
    const duration = c.metadata?.durationSeconds;
    return typeof start === 'number' && typeof duration === 'number' && seconds >= start && seconds < start + duration;
  });
}
